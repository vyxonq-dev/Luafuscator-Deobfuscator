using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    
    public class LuaConstants
    {
        public int TypeMath      { get; } = 5;   
        public int TypePcall     { get; } = 8;   
        public int TypeType      { get; } = 8;   
        public int TypeCoroutine { get; } = 5;   
        public int ToStringTrue  { get; } = 4;   
        public int TableSize     { get; } = 3;   

        private static readonly Regex LambdaPattern = new Regex(
            @"\(\s*function\s*\([^)]*\)\s*return\s+[a-z]\s*\*\s*[a-z]\s*\+\s*[a-z]\s*end\s*\)\s*" +
            @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)\s*-\s*\(\s*(-?\d+)\s*\)",
            RegexOptions.Compiled);

        private static readonly Regex LambdaPatternNoSub = new Regex(
            @"\(\s*function\s*\([^)]*\)\s*return\s+[a-z]\s*\*\s*[a-z]\s*\+\s*[a-z]\s*end\s*\)\s*" +
            @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)",
            RegexOptions.Compiled);

        public int? EvalSeedExpr(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return null;

            string e = expr.Trim();

            var lm = LambdaPattern.Match(e);
            if (lm.Success)
            {
                long a = long.Parse(lm.Groups[1].Value);
                long b = long.Parse(lm.Groups[2].Value);
                long c = long.Parse(lm.Groups[3].Value);
                long k = long.Parse(lm.Groups[4].Value);
                return (int)((a * c + b) - k);
            }
            var lmns = LambdaPatternNoSub.Match(e);
            if (lmns.Success)
            {
                long a = long.Parse(lmns.Groups[1].Value);
                long b = long.Parse(lmns.Groups[2].Value);
                long c = long.Parse(lmns.Groups[3].Value);
                return (int)(a * c + b);
            }

            e = e.Replace("#type(coroutine)", TypeCoroutine.ToString());
            e = e.Replace("#type(pcall)",     TypePcall.ToString());
            e = e.Replace("#type(math)",      TypeMath.ToString());
            e = e.Replace("#type(type)",      TypeType.ToString());
            e = e.Replace("#tostring(true)",  ToStringTrue.ToString());
            e = e.Replace("#{1,2,3}",         TableSize.ToString());
            e = e.Replace("#{1;2;3}",         TableSize.ToString());
            e = e.Replace("#{1;2,3}",         TableSize.ToString());
            e = e.Replace("#{1,2;3}",         TableSize.ToString());
            
            e = Regex.Replace(e, @"#\{[^}]{1,20}\}", TableSize.ToString());

            return SimpleArithEval(e);
        }

        private static int? SimpleArithEval(string expr)
        {
            try
            {
                var tokens = Tokenize(expr.Replace(" ", ""));
                int pos    = 0;
                return ParseExpr(tokens, ref pos);
            }
            catch { return null; }
        }

        private static List<string> Tokenize(string s)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '(' || c == ')' || c == '+' || c == '*' || c == '/')
                { tokens.Add(c.ToString()); i++; }
                else if (c == '-')
                { tokens.Add(c.ToString()); i++; }
                else if (char.IsDigit(c))
                {
                    int j = i;
                    while (j < s.Length && char.IsDigit(s[j])) j++;
                    tokens.Add(s.Substring(i, j - i));
                    i = j;
                }
                else { i++; }   
            }
            return tokens;
        }

        private static int ParseExpr(List<string> t, ref int pos)
        {
            int left = ParseTerm(t, ref pos);
            while (pos < t.Count && (t[pos] == "+" || t[pos] == "-"))
            {
                string op = t[pos++];
                int right = ParseTerm(t, ref pos);
                left = op == "+" ? left + right : left - right;
            }
            return left;
        }

        private static int ParseTerm(List<string> t, ref int pos)
        {
            int left = ParseFactor(t, ref pos);
            while (pos < t.Count && (t[pos] == "*" || t[pos] == "/"))
            {
                string op = t[pos++];
                int right = ParseFactor(t, ref pos);
                left = op == "*" ? left * right : (right == 0 ? 0 : left / right);
            }
            return left;
        }

        private static int ParseFactor(List<string> t, ref int pos)
        {
            if (pos >= t.Count) return 0;
            string tok = t[pos];
            if (tok == "-") { pos++; return -ParseFactor(t, ref pos); }
            if (tok == "(")
            {
                pos++;
                int val = ParseExpr(t, ref pos);
                if (pos < t.Count && t[pos] == ")") pos++;
                return val;
            }
            if (int.TryParse(tok, out int n)) { pos++; return n; }
            pos++;
            return 0;
        }
    }
}
