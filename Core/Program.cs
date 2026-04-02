using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LuafuscatorDeobf
{
    class Program
    {
        static void Main(string[] args)
        {
            PrintBanner();

            bool analyzeMode   = args.Any(a => a == "--analyze"   || a == "-a");
            bool verboseMode   = args.Any(a => a == "--verbose"   || a == "-v");
            bool noLfr         = args.Any(a => a == "--no-lfr");
            bool noFold        = args.Any(a => a == "--no-fold");
            bool noAst         = args.Any(a => a == "--no-ast");
            bool printableOnly = args.Any(a => a == "--printable" || a == "-p");
            bool quietMode     = args.Any(a => a == "--quiet"     || a == "-q");
            string inputPath   = args.FirstOrDefault(a => !a.StartsWith("-"));

            if (inputPath == null)
            {
                Console.Write("Enter path to obfuscated .lua file: ");
                inputPath = Console.ReadLine()?.Trim().Trim('"');
            }

            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            {
                Err($"File not found: {inputPath}");
                PrintUsage();
                Environment.Exit(1);
            }

            string code = File.ReadAllText(inputPath);
            Info($"Loaded: {inputPath}  ({code.Length:N0} chars, {code.Split('\n').Length:N0} lines)");

            var luaConsts = new LuaConstants();

            if (verboseMode)
            {
                Info("Lua runtime constants:");
                Console.WriteLine($"    #type(math)      = {luaConsts.TypeMath}");
                Console.WriteLine($"    #type(pcall)     = {luaConsts.TypePcall}");
                Console.WriteLine($"    #type(type)      = {luaConsts.TypeType}");
                Console.WriteLine($"    #type(coroutine) = {luaConsts.TypeCoroutine}");
                Console.WriteLine($"    #tostring(true)  = {luaConsts.ToStringTrue}");
                Console.WriteLine($"    #{{1,2,3}}         = {luaConsts.TableSize}");
                Console.WriteLine();
            }

            var decoder = new LuaStringDecoder(luaConsts);
            var parser  = new GCallParser(decoder);
            var results = parser.DecodeAll(code);

            // Disable DEBUG

            //Info($"Aliases detected       : {string.Join(", ", parser.DetectedAliases)}");
            //Info($"Total calls decoded: {results.Count}");

            if (parser.FailedDecodes > 0 || parser.SkippedCalls > 0)
                Warn($"Failed: {parser.FailedDecodes}  |  Skipped (wrong arg count): {parser.SkippedCalls}");

            if (verboseMode)
                foreach (var kv in parser.AliasCallCounts.OrderByDescending(k => k.Value))
                    Console.WriteLine($"    {kv.Key,-32} -> {kv.Value} calls");

            Console.WriteLine();

            foreach (var r in results)
                r.Category = StringClassifier.Classify(r.Decoded);

            var display = printableOnly
                ? results.Where(r => LuaStringDecoder.IsPrintable(r.Decoded)).ToList()
                : results;

            if (!quietMode)
            {
                int shown = 0;
                foreach (var r in display)
                {
                    if (r.Decoded.Length <= 3 && !verboseMode) continue;
                    if (!LuaStringDecoder.IsPrintable(r.Decoded) && !verboseMode) continue;
                    shown++;
                    Console.ForegroundColor = StringClassifier.CategoryColor(r.Category);
                    Console.Write($"  [{StringClassifier.CategoryLabel(r.Category),-14}] ");
                    Console.ResetColor();
                    string preview = r.Decoded.Length > 120 ? r.Decoded.Substring(0, 120) + "..." : r.Decoded;
                    Console.WriteLine(preview);
                }
                //Console.WriteLine();
                Info($"Displayed {shown} strings (--verbose to show all, --quiet to suppress)\n");
            }

            if (analyzeMode)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("=== Analysis Summary ===");
                Console.ResetColor();

                foreach (var grp in results.GroupBy(r => r.Category).OrderByDescending(g => g.Count()))
                {
                    Console.ForegroundColor = StringClassifier.CategoryColor(grp.Key);
                    Console.Write($"  {StringClassifier.CategoryLabel(grp.Key),-20}");
                    Console.ResetColor();
                    Console.Write($": {grp.Count(),4}  ");
                    var examples = grp
                        .Where(r => r.Decoded.Length > 2 && LuaStringDecoder.IsPrintable(r.Decoded))
                        .Take(5)
                        .Select(r => r.Decoded.Length > 28 ? r.Decoded.Substring(0, 28) + "..." : r.Decoded);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(string.Join(", ", examples));
                    Console.ResetColor();
                }

                Console.WriteLine();

                var execApis = results.Where(r => r.Category == StringCategory.RobloxExecutor).ToList();
                if (execApis.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  WARNING: EXECUTOR APIs DETECTED ({execApis.Count}):");
                    Console.ResetColor();
                    foreach (var r in execApis)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"       {r.Decoded}");
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }
            }

            LfrTableResolver lfr = null;
            string inlineSource;

            {
                var tmpSb  = new StringBuilder(code);
                var sorted = new List<DecodeResult>(results);
                sorted.Sort((a, b) => b.Position.CompareTo(a.Position));

                foreach (var r in sorted)
                {
                    int pos = tmpSb.ToString().IndexOf(r.OriginalCall, StringComparison.Ordinal);
                    if (pos < 0) continue;
                    string esc = r.Decoded
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\0", "\\0");
                    tmpSb.Remove(pos, r.OriginalCall.Length);
                    tmpSb.Insert(pos, $"\"{esc}\"");
                }

                inlineSource = tmpSb.ToString();

                if (!noLfr)
                {
                    lfr = new LfrTableResolver();
                    lfr.ParseAssignments(inlineSource);
                    if (lfr.Count > 0) Info($"_LFR[] table entries resolved: {lfr.Count}");
                }
            }

            ConstantFolder folder      = null;
            string         foldedSource = null;

            if (!noFold)
            {
                folder = new ConstantFolder();
                string preFold = inlineSource;

                if (lfr != null && lfr.Count > 0)
                    preFold = lfr.SubstituteReferences(preFold);

                foldedSource = folder.FoldAndRename(preFold);

                //Info($"Lambda expressions folded  : {folder.LambdasFolded}");
                //Info($"Constant vars folded       : {folder.VarConstants.Count}");
                //Info($"_LFR chain entries labeled : {folder.LfrChain.Count}");

                if (verboseMode)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("  -- Resolved constant vars --");
                    Console.ResetColor();

                    foreach (var kv in folder.VarConstants)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write($"    {kv.Key,-36}");
                        Console.ResetColor();
                        Console.Write($" = {kv.Value}");

                        if (folder.VarRenames.TryGetValue(kv.Key, out string rn))
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write($"  ->  {rn}");
                            Console.ResetColor();
                        }

                        Console.WriteLine();
                    }
                }
            }

            string astSource   = null;
            int    astErrors   = 0;
            int    astRenames  = 0;
            string sourceForAst = foldedSource ?? inlineSource;

            if (!noAst && sourceForAst != null)
            {

                try
                {
                    var lexer     = new LuaLexer(sourceForAst);
                    var tokens    = lexer.Tokenize();
                    var luaParser = new LuaParser(tokens);
                    var ast       = luaParser.ParseBlock();
                    astErrors     = luaParser.Errors.Count;

                    if (verboseMode && astErrors > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  Parser messages ({astErrors}):");
                        foreach (var e in luaParser.Errors.Take(20))
                            Console.WriteLine($"    {e}");
                        Console.ResetColor();
                    }

                    var renamer = new LuaRenamer();
                    if (folder != null) renamer.KnownConstants = folder.VarConstants;
                    renamer.Analyse(ast);
                    astRenames = renamer.Renames.Count;

                    var unparser = new LuaUnparser(renamer.Renames);
                    astSource    = unparser.Emit(ast);

                    if (verboseMode && renamer.Renames.Count > 0)
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine("  -- AST renames (first 60) --");
                        Console.ResetColor();

                        foreach (var kv in renamer.Renames.Take(60))
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write($"    {kv.Key,-36}");
                            Console.ResetColor();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($" -> {kv.Value}");
                            Console.ResetColor();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Warn($"AST pipeline failed: {ex.Message} -- falling back to folded output");
                    astSource = null;
                }
            }

            string outDir    = Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? ".";
            string finalPath = Path.Combine(outDir, "deobfuscated.lua");

            string finalSource = astSource ?? foldedSource ?? inlineSource;
            OutputWriter.WriteFinalOutput(finalPath, finalSource, results, parser, folder, astRenames, astErrors);

            Console.WriteLine();
            Ok("Saved output file:");
            PrintFile(finalPath, "deobfuscated output");

            Console.WriteLine();
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("|  Luafuscator 1.0.8 Deobfuscator");
            Console.ResetColor();
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: LuafuscatorDeobf [file.lua] [options]");
            Console.WriteLine("  --analyze/-a     Categorised summary + executor API warnings");
            Console.WriteLine("  --verbose/-v     Show all decoded tokens + rename details");
            Console.WriteLine("  --quiet/-q       Suppress per-string output");
            Console.WriteLine("  --printable/-p   Only show printable ASCII strings");
            Console.WriteLine("  --no-lfr         Skip _LFR[] table resolution");
            Console.WriteLine("  --no-fold        Skip constant folding");
            Console.WriteLine("  --no-ast         Skip AST lexer/parser/renamer/unparser pass");
            Console.WriteLine();
            Console.WriteLine("Output (written next to input file):");
            Console.WriteLine("  deobfuscated.lua     fully deobfuscated source");
        }

        static void PrintFile(string path, string desc)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("   -> ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(Path.GetFileName(path));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  ({desc})");
            Console.ResetColor();
        }

        static void Info(string msg)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[*] {msg}");
            Console.ResetColor();
        }

        static void Ok(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[+] {msg}");
            Console.ResetColor();
        }

        static void Err(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[!] {msg}");
            Console.ResetColor();
        }

        static void Warn(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[~] {msg}");
            Console.ResetColor();
        }
    }
}
