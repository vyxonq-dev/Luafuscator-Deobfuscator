using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    
    public static class LocalVarTracer
    {
        
        private static readonly Regex ObfVarName = new Regex(
            @"^_[A-Za-z0-9_]{5,}$",
            RegexOptions.Compiled);

        private static readonly Regex LocalSingleAssign = new Regex(
            @"local\s+(_[A-Za-z0-9_]{5,})\s*=\s*(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')",
            RegexOptions.Compiled);

        private static readonly Regex ReAssign = new Regex(
            @"(?<![A-Za-z0-9_])(_[A-Za-z0-9_]{5,})\s*=\s*(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')",
            RegexOptions.Compiled);

        private static readonly Regex LocalMultiAssign = new Regex(
            @"local\s+((?:_[A-Za-z0-9_]{5,}\s*,\s*)*_[A-Za-z0-9_]{5,})\s*=\s*(.+)",
            RegexOptions.Compiled);

        public static string SubstituteVars(string source, out int substitutionCount)
        {
            substitutionCount = 0;

            var varMap = new Dictionary<string, string>(StringComparer.Ordinal);
            CollectMappings(source, varMap);

            if (varMap.Count == 0)
                return source;

            var varNames = new List<string>(varMap.Keys);
            varNames.Sort((a, b) => b.Length.CompareTo(a.Length));

            string result = source;
            foreach (var varName in varNames)
            {
                string value = varMap[varName];
                
                var usePattern = new Regex(
                    @"(?<![A-Za-z0-9_\.])" + Regex.Escape(varName) + @"(?![A-Za-z0-9_\s]*=(?!=))",
                    RegexOptions.Compiled);

                string before = result;
                result = usePattern.Replace(result, value);
                
                int diff = CountOccurrences(before, varName) - CountOccurrences(result, varName);
                substitutionCount += diff;
            }

            return result;
        }

        private static void CollectMappings(string source, Dictionary<string, string> varMap)
        {
            
            foreach (Match m in LocalSingleAssign.Matches(source))
            {
                string name = m.Groups[1].Value;
                string val  = m.Groups[2].Value; 
                varMap[name] = val;
            }

            foreach (Match m in LocalMultiAssign.Matches(source))
            {
                string namesPart = m.Groups[1].Value;
                string valsPart  = m.Groups[2].Value.Trim();

                var names = SplitIdents(namesPart);
                var vals  = SplitStringLiterals(valsPart);

                for (int i = 0; i < names.Count && i < vals.Count; i++)
                {
                    if (ObfVarName.IsMatch(names[i]))
                        varMap[names[i]] = vals[i];
                }
            }

            foreach (Match m in ReAssign.Matches(source))
            {
                string name = m.Groups[1].Value;
                string val  = m.Groups[2].Value;
                
                if (varMap.ContainsKey(name) || ObfVarName.IsMatch(name))
                    varMap[name] = val;
            }
        }

        private static List<string> SplitIdents(string s)
        {
            var result = new List<string>();
            foreach (var part in s.Split(','))
            {
                string t = part.Trim();
                if (t.Length > 0) result.Add(t);
            }
            return result;
        }

        private static List<string> SplitStringLiterals(string s)
        {
            var result = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                
                while (i < s.Length && (s[i] == ',' || s[i] == ' ' || s[i] == '\t')) i++;
                if (i >= s.Length) break;

                char q = s[i];
                if (q == '"' || q == '\'')
                {
                    int start = i;
                    i++;
                    while (i < s.Length)
                    {
                        if (s[i] == '\\') { i += 2; continue; }
                        if (s[i] == q) { i++; break; }
                        i++;
                    }
                    result.Add(s.Substring(start, i - start));
                }
                else
                {
                    
                    while (i < s.Length && s[i] != ',') i++;
                    result.Add(""); 
                }
            }
            return result;
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, pos = 0;
            while ((pos = haystack.IndexOf(needle, pos, StringComparison.Ordinal)) >= 0)
            {
                count++;
                pos += needle.Length;
            }
            return count;
        }
    }

    public static class FunctionAliasResolver
    {
        
        private static readonly Regex LocalFuncAlias = new Regex(
            @"local\s+(_[A-Za-z0-9_]{5,})\s*=\s*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*|\[""[^""]*""\]|\['[^']*'\])+)",
            RegexOptions.Compiled);

        public static string SubstituteFunctionAliases(string source, out int count)
        {
            count = 0;
            var aliasMap = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match m in LocalFuncAlias.Matches(source))
            {
                string name = m.Groups[1].Value;
                string expr = m.Groups[2].Value;
                aliasMap[name] = expr;
            }

            if (aliasMap.Count == 0) return source;

            string result = source;
            var varNames = new List<string>(aliasMap.Keys);
            varNames.Sort((a, b) => b.Length.CompareTo(a.Length));

            foreach (var varName in varNames)
            {
                string replacement = aliasMap[varName];
                
                var pattern = new Regex(
                    @"(?<![A-Za-z0-9_])" + Regex.Escape(varName) + @"(?=\s*\()",
                    RegexOptions.Compiled);
                int before = Regex.Matches(result, Regex.Escape(varName)).Count;
                result = pattern.Replace(result, replacement);
                int after = Regex.Matches(result, Regex.Escape(varName)).Count;
                count += (before - after);
            }

            return result;
        }
    }
}
