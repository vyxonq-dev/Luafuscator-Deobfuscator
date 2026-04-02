using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    
    public class ConstantFolder
    {
        
        public Dictionary<string, long> VarConstants { get; } = new Dictionary<string, long>();

        public Dictionary<string, string> VarRenames { get; } = new Dictionary<string, string>();

        public Dictionary<int, string> LfrChain { get; } = new Dictionary<int, string>();

        public int LambdasFolded { get; private set; }

        private static readonly Regex LambdaFull = new Regex(
            @"\(\s*\(function\s*\([^)]*\)\s*return\s+\w+\s*\*\s*\w+\s*\+\s*\w+\s*end\s*\)\s*" +
            @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)\s*-\s*\(\s*(-?\d+)\s*\)\s*\)",
            RegexOptions.Compiled);

        private static readonly Regex LambdaNoSub = new Regex(
            @"\(\s*\(function\s*\([^)]*\)\s*return\s+\w+\s*\*\s*\w+\s*\+\s*\w+\s*end\s*\)\s*" +
            @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)\s*\)",
            RegexOptions.Compiled);

        private static readonly Regex ConstArrayDecl = new Regex(
            @"local\s+(_[A-Za-z0-9_]+)\s*=\s*\{([^}]{1,8000})\}",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex GetterDecl = new Regex(
            @"local\s+(_[A-Za-z0-9_]+)\s*=\s*function\s*\(\s*(\w+)\s*\)\s*\n?\s*return\s+(_[A-Za-z0-9_]+)\s*\[\s*\2\s*\]\s*\n?\s*end",
            RegexOptions.Compiled);

        private static readonly Regex GetterCall = new Regex(
            @"local\s+(_[A-Za-z0-9_]+)\s*=\s*({GETTER})\s*\(\s*(\d+)\s*\)",
            RegexOptions.Compiled);

        private static readonly Regex IifeClosure = new Regex(
            @"local\s+(_[A-Za-z0-9_]+)\s*=\s*\(function\s*\(\s*\)\s*" +
            @"local\s+\w+\s*=\s*(" +
                @"\(\s*\(function\([^)]*\)\s*return\s+\w+\s*\*\s*\w+\s*\+\s*\w+\s*end\s*\)\s*\(-?\d+\s*,\s*-?\d+\s*,\s*-?\d+\s*\)\s*-\s*\(\s*-?\d+\s*\)\s*\)" +
            @")\s*" +
            @"return\s+function\s*\(\s*\)\s*return\s+\w+\s*end\s*end\s*\)\s*\(\s*\)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex LfrEnvAssign = new Regex(
            @"_LFR\[(\d+)\]\s*=\s*_[A-Za-z0-9_]+\[""([^""]+)""\]",
            RegexOptions.Compiled);

        private static readonly Regex LfrChainAssign = new Regex(
            @"_LFR\[(\d+)\]\s*=\s*_LFR\[(\d+)\]\[""([^""]+)""\]",
            RegexOptions.Compiled);

        public string FoldAndRename(string source)
        {
            
            source = FoldLambdas(source);

            DiscoverConstantVars(source);

            DiscoverIifeVars(source);

            BuildRenameMap();

            ResolveLfrChain(source);

            source = ReplaceGetterCalls(source);

            source = RenameVars(source);

            source = AnnotateLfr(source);

            source = StripBoilerplate(source);

            return source;
        }

        public string FoldLambdas(string source)
        {
            
            int countFull = LambdaFull.Matches(source).Count;
            source = LambdaFull.Replace(source, m =>
            {
                long a = long.Parse(m.Groups[1].Value);
                long b = long.Parse(m.Groups[2].Value);
                long c = long.Parse(m.Groups[3].Value);
                long k = long.Parse(m.Groups[4].Value);
                return (a * c + b - k).ToString();
            });

            int countNoSub = LambdaNoSub.Matches(source).Count;
            
            source = LambdaNoSub.Replace(source, m =>
            {
                long a = long.Parse(m.Groups[1].Value);
                long b = long.Parse(m.Groups[2].Value);
                long c = long.Parse(m.Groups[3].Value);
                return (a * c + b).ToString();
            });

            LambdasFolded += countFull + countNoSub;
            return source;
        }

        private void DiscoverConstantVars(string source)
        {
            
            string getterName = null;
            string arrayName  = null;

            var gm = GetterDecl.Match(source);
            if (gm.Success)
            {
                getterName = gm.Groups[1].Value;
                arrayName  = gm.Groups[3].Value;
            }
            else
            {
                
                var alt = Regex.Match(source,
                    @"local\s+(_[A-Za-z0-9_]+)\s*=\s*function\s*\(\s*(\w+)\s*\)\s*return\s+(_[A-Za-z0-9_]+)\s*\[\2\]\s*end");
                if (alt.Success)
                {
                    getterName = alt.Groups[1].Value;
                    arrayName  = alt.Groups[3].Value;
                }
            }

            if (getterName == null) return;

            var arrMatch = Regex.Match(source,
                @"local\s+" + Regex.Escape(arrayName) + @"\s*=\s*\{([^}]{1,8000})\}",
                RegexOptions.Singleline);

            if (!arrMatch.Success) return;

            var values = ParseArrayValues(arrMatch.Groups[1].Value);

            var callPat = new Regex(
                @"local\s+(_[A-Za-z0-9_]+)\s*=\s*" + Regex.Escape(getterName) + @"\s*\(\s*(\d+)\s*\)");

            foreach (Match m in callPat.Matches(source))
            {
                int idx = int.Parse(m.Groups[2].Value);
                if (idx >= 1 && idx <= values.Count)
                    VarConstants[m.Groups[1].Value] = values[idx - 1];
            }
        }

        private static List<long> ParseArrayValues(string arrayBody)
        {
            var list = new List<long>();
            
            foreach (var tok in Regex.Split(arrayBody, @"[,\n\r]+"))
            {
                string t = tok.Trim();
                if (t.Length == 0) continue;
                if (long.TryParse(t, out long v))
                    list.Add(v);
            }
            return list;
        }

        private void DiscoverIifeVars(string source)
        {
            
            var iifeSimple = new Regex(
                @"local\s+(_[A-Za-z0-9_]+)\s*=\s*\(function\s*\(\s*\)\s*" +
                @"local\s+\w+\s*=\s*(-?\d+)\s*" +
                @"return\s+function\s*\(\s*\)\s*return\s+\w+\s*end\s*end\s*\)\s*\(\s*\)",
                RegexOptions.Singleline);

            foreach (Match m in iifeSimple.Matches(source))
            {
                if (long.TryParse(m.Groups[2].Value, out long val))
                    VarConstants[m.Groups[1].Value] = val;
            }
        }

        private void BuildRenameMap()
        {
            foreach (var kv in VarConstants)
            {
                
                string name = kv.Value >= 0 && kv.Value <= 9999
                    ? $"__k{kv.Value}"
                    : $"__k_{kv.Value}";
                VarRenames[kv.Key] = name;
            }
        }

        private void ResolveLfrChain(string source)
        {
            
            foreach (Match m in LfrEnvAssign.Matches(source))
            {
                int idx = int.Parse(m.Groups[1].Value);
                LfrChain[idx] = m.Groups[2].Value;
            }

            bool changed = true;
            int passes = 0;
            while (changed && passes < 10)
            {
                changed = false;
                foreach (Match m in LfrChainAssign.Matches(source))
                {
                    int dst = int.Parse(m.Groups[1].Value);
                    int src = int.Parse(m.Groups[2].Value);
                    string key = m.Groups[3].Value;
                    if (!LfrChain.ContainsKey(dst))
                    {
                        string srcName = LfrChain.TryGetValue(src, out string sv) ? sv : $"_LFR[{src}]";
                        LfrChain[dst] = $"{srcName}.{key}";
                        changed = true;
                    }
                }
                passes++;
            }
        }

        private string ReplaceGetterCalls(string source)
        {
            
            var gm = GetterDecl.Match(source);
            string getterName = gm.Success ? gm.Groups[1].Value : null;

            if (getterName == null)
            {
                var alt = Regex.Match(source,
                    @"local\s+(_[A-Za-z0-9_]+)\s*=\s*function\s*\(\s*(\w+)\s*\)\s*return\s+(_[A-Za-z0-9_]+)\s*\[\2\]\s*end");
                if (alt.Success) getterName = alt.Groups[1].Value;
            }

            if (getterName == null) return source;

            var callPat = new Regex(
                @"(local\s+_[A-Za-z0-9_]+\s*=\s*)" + Regex.Escape(getterName) + @"\s*\(\s*(\d+)\s*\)");

            return callPat.Replace(source, m =>
            {
                
                return m.Groups[1].Value + m.Groups[2].Value + " --[[getter]]";
            });
        }

        private string RenameVars(string source)
        {
            if (VarRenames.Count == 0) return source;

            var sb = new StringBuilder(source);

            var entries = new List<KeyValuePair<string, string>>(VarRenames);
            entries.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));

            foreach (var kv in entries)
            {
                
                var pat = new Regex(@"(?<![A-Za-z0-9_])" + Regex.Escape(kv.Key) + @"(?![A-Za-z0-9_])");
                string replaced = pat.Replace(sb.ToString(), kv.Value);
                sb.Clear();
                sb.Append(replaced);
            }

            return sb.ToString();
        }

        private string AnnotateLfr(string source)
        {
            if (LfrChain.Count == 0) return source;

            return Regex.Replace(source, @"_LFR\[(\d+)\]", m =>
            {
                int idx = int.Parse(m.Groups[1].Value);
                if (LfrChain.TryGetValue(idx, out string label))
                    return $"_LFR[{idx}]--[[{label}]]";
                return m.Value;
            });
        }

        private static string StripBoilerplate(string source)
        {
            
            source = Regex.Replace(source,
                @"(local\s+_[A-Za-z0-9_]+\s*=\s*\{(?:\s*-?\d+\s*,?\s*){2,}\s*\})",
                m => $"--[[ CONST_ARRAY: {m.Value.Substring(0, Math.Min(80, m.Value.Length))}... ]]",
                RegexOptions.Singleline);

            source = Regex.Replace(source,
                @"local\s+(__k[A-Za-z0-9_]*)\s*=\s*\(function\s*\(\s*\)\s*local\s+\w+\s*=\s*-?\d+\s*return\s+function\s*\(\s*\)\s*return\s+\w+\s*end\s*end\s*\)\s*\(\s*\)",
                m => $"-- {m.Groups[1].Value} already resolved above",
                RegexOptions.Singleline);

            source = Regex.Replace(source,
                @"local\s+_[A-Za-z0-9_]+\s*=\s*function\s*\(\s*\w+\s*\)\s*\n?\s*return\s+_[A-Za-z0-9_]+\s*\[\s*\w+\s*\]\s*\n?\s*end",
                "-- [getter removed: inlined above]");

            return source;
        }

        public string GetReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  Constant vars resolved : {VarConstants.Count}");
            foreach (var kv in VarConstants)
                sb.AppendLine($"    {kv.Key,-36} = {kv.Value}  →  renamed to  {VarRenames.GetValueOrDefault(kv.Key)}");

            sb.AppendLine();
            sb.AppendLine($"  _LFR chain entries     : {LfrChain.Count}");
            foreach (var kv in LfrChain)
                sb.AppendLine($"    _LFR[{kv.Key,-6}] = {kv.Value}");

            return sb.ToString();
        }
    }
}
