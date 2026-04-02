using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    
    public class GCallParser
    {
        private readonly LuaStringDecoder _decoder;
        private readonly LuaConstants     _consts;

        private static readonly Regex AliasDefPattern = new Regex(
            @"local\s+function\s+(_[A-Za-z0-9_]{6,})\s*\(",
            RegexOptions.Compiled);

        private readonly HashSet<string> _knownAliases = new HashSet<string> { "g" };

        public Dictionary<string, int> AliasCallCounts { get; } = new Dictionary<string, int>();
        public int FailedDecodes  { get; private set; }
        public int SkippedCalls   { get; private set; }

        public GCallParser(LuaStringDecoder decoder)
        {
            _decoder = decoder;
            _consts  = new LuaConstants();
        }

        public void DiscoverAliases(string code)
        {
            foreach (Match m in AliasDefPattern.Matches(code))
            {
                string name      = m.Groups[1].Value;
                int    bodyStart = m.Index;
                int    peekLen   = Math.Min(800, code.Length - bodyStart);
                string peek      = code.Substring(bodyStart, peekLen);

                bool hasRollingState = peek.Contains("% 256");
                bool hasAccumulator  = peek.Contains("= \"\"") || peek.Contains("=\"\"");
                bool hasCharTable    = peek.Contains("[_") && peek.Contains("]");

                if (hasRollingState && (hasAccumulator || hasCharTable))
                    _knownAliases.Add(name);
            }
        }

        public List<DecodeResult> DecodeAll(string code)
        {
            DiscoverAliases(code);

            var results    = new List<DecodeResult>();
            int searchFrom = 0;

            while (true)
            {
                int gPos = FindNextGCall(code, searchFrom, out string matchedAlias);
                if (gPos < 0) break;

                int parenStart = gPos;
                string args = ExtractBalancedParens(code, parenStart, out int parenEnd);
                if (args == null) { searchFrom = parenStart + 1; continue; }

                searchFrom = parenEnd + 1;

                var parts = SplitTopLevelArgs(args);
                if (parts.Count != 4) { SkippedCalls++; continue; }

                var data = ParseIntArray(parts[0]);
                if (data == null) { SkippedCalls++; continue; }

                if (data.Count == 0)
                {
                    results.Add(new DecodeResult
                    {
                        OriginalCall = matchedAlias + "(" + args + ")",
                        Decoded      = "",
                        Position     = gPos - matchedAlias.Length - 1,
                        Alias        = matchedAlias
                    });
                    IncrementAlias(matchedAlias);
                    continue;
                }

                var lookup = ParseLookupTable(parts[1]);
                if (lookup == null) { SkippedCalls++; continue; }

                int? seed  = _consts.EvalSeedExpr(parts[2]);
                if (seed == null) { FailedDecodes++; continue; }

                int? extra = _consts.EvalSeedExpr(parts[3]);
                if (extra == null) { FailedDecodes++; continue; }

                string decoded = _decoder.Decode(data, lookup, seed.Value, extra.Value);
                if (decoded == null) { FailedDecodes++; continue; }

                results.Add(new DecodeResult
                {
                    OriginalCall = matchedAlias + "(" + args + ")",
                    Decoded      = decoded,
                    Position     = gPos - matchedAlias.Length - 1,
                    Alias        = matchedAlias
                });
                IncrementAlias(matchedAlias);
            }

            return results;
        }

        public IEnumerable<string> DetectedAliases => _knownAliases;

        private void IncrementAlias(string alias)
        {
            if (!AliasCallCounts.ContainsKey(alias)) AliasCallCounts[alias] = 0;
            AliasCallCounts[alias]++;
        }

        private int FindNextGCall(string code, int from, out string matchedAlias)
        {
            int best     = -1;
            matchedAlias = "g";

            foreach (string alias in _knownAliases)
            {
                int pos = FindAlias(code, alias, from);
                if (pos >= 0 && (best < 0 || pos < best))
                {
                    best         = pos;
                    matchedAlias = alias;
                }
            }

            return best;
        }

        private static int FindAlias(string code, string alias, int from)
        {
            string needle = alias + "(";
            int pos = from;
            while (true)
            {
                int idx = code.IndexOf(needle, pos, StringComparison.Ordinal);
                if (idx < 0) return -1;

                bool prevOk = idx == 0 || !IsIdentChar(code[idx - 1]);
                if (prevOk)
                    return idx + alias.Length;

                pos = idx + 1;
            }
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        public static string ExtractBalancedParens(string code, int openPos, out int closePos)
        {
            closePos = -1;
            if (openPos >= code.Length || code[openPos] != '(') return null;

            int depth = 1;
            int i     = openPos + 1;
            bool inStr = false;
            char strChar = '"';

            while (i < code.Length && depth > 0)
            {
                char c = code[i];

                if (inStr)
                {
                    if (c == '\\') { i += 2; continue; }
                    if (c == strChar) inStr = false;
                }
                else
                {
                    if (c == '"' || c == '\'') { inStr = true; strChar = c; }
                    else if (c == '(') depth++;
                    else if (c == ')') depth--;
                }
                i++;
            }

            if (depth != 0) return null;
            closePos = i - 1;
            return code.Substring(openPos + 1, closePos - openPos - 1);
        }

        public static List<string> SplitTopLevelArgs(string s)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            bool inStr = false;
            char strChar = '"';

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == strChar) inStr = false;
                }
                else
                {
                    if (c == '"' || c == '\'') { inStr = true; strChar = c; }
                    else if (c == '(' || c == '{') depth++;
                    else if (c == ')' || c == '}') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        parts.Add(s.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                }
            }
            parts.Add(s.Substring(start).Trim());
            return parts;
        }

        public static List<int> ParseIntArray(string s)
        {
            s = s.Trim();
            if (!s.StartsWith("{") || !s.EndsWith("}")) return null;
            s = s.Substring(1, s.Length - 2);

            var result = new List<int>();
            if (s.Trim().Length == 0) return result;

            foreach (var tok in Regex.Split(s, @"[,;]+"))
            {
                string t = tok.Trim();
                if (t.Length == 0) continue;
                if (int.TryParse(t, out int v))
                    result.Add(v);
                else
                    return null;
            }
            return result;
        }

        public static Dictionary<int, int> ParseLookupTable(string s)
        {
            s = s.Trim();
            if (!s.StartsWith("{") || !s.EndsWith("}")) return null;

            var dict   = new Dictionary<int, int>();
            var kvPair = new Regex(@"\[(\d+)\]\s*=\s*(\d+)");

            foreach (Match m in kvPair.Matches(s))
            {
                int key = int.Parse(m.Groups[1].Value);
                int val = int.Parse(m.Groups[2].Value);
                dict[key] = val;
            }
            return dict;
        }
    }

    public class DecodeResult
    {
        public string OriginalCall { get; set; }
        public string Decoded      { get; set; }
        public int    Position     { get; set; }
        public string Alias        { get; set; } = "g";
        public StringCategory Category { get; set; } = StringCategory.Unknown;
    }

    public enum StringCategory
    {
        Unknown,
        RobloxService,
        RobloxMethod,
        RobloxEvent,
        RobloxProperty,
        LuaBuiltin,
        LuafuscatorMarker,
        SynapseSNC,         
        RobloxExecutor,     
        MetaMethod,         
        GenericString,
        Number,
        ShortToken,
        URL,
        PatternString,      
    }
}
