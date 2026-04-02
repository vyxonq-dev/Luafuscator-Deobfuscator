using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    
    public class LfrTableResolver
    {
        
        private readonly Dictionary<int, string> _table = new Dictionary<int, string>();

        private readonly string _varName;

        private static readonly Regex AssignPattern;

        static LfrTableResolver()
        {
            
            AssignPattern = new Regex(
                @"_LFR\[(\d+)\]\s*=\s*([""'])((?:[^""'\\]|\\.)*?)\2",
                RegexOptions.Compiled);
        }

        public LfrTableResolver(string varName = "_LFR")
        {
            _varName = varName;
        }

        public void ParseAssignments(string inlineSource)
        {
            foreach (Match m in AssignPattern.Matches(inlineSource))
            {
                if (int.TryParse(m.Groups[1].Value, out int idx))
                {
                    string val = UnescapeLuaString(m.Groups[3].Value);
                    _table[idx] = val;
                }
            }
        }

        public bool TryGet(int index, out string value) =>
            _table.TryGetValue(index, out value);

        public int Count => _table.Count;

        public IEnumerable<KeyValuePair<int, string>> All() => _table;

        public string SubstituteReferences(string source)
        {
            
            var sb = new StringBuilder(source);
            
            var entries = new List<KeyValuePair<int, string>>(_table);
            entries.Sort((a, b) => b.Key.CompareTo(a.Key));

            foreach (var kv in entries)
            {
                string needle      = $"_LFR[{kv.Key}]";
                string replacement = $"\"{EscapeLuaString(kv.Value)}\"";
                sb.Replace(needle, replacement);
            }
            return sb.ToString();
        }

        private static string UnescapeLuaString(string s)
        {
            return s.Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\\\", "\\")
                    .Replace("\\\"", "\"")
                    .Replace("\\'", "'")
                    .Replace("\\0", "\0");
        }

        private static string EscapeLuaString(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\0", "\\0");
        }
    }
}
