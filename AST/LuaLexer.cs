using System;
using System.Collections.Generic;
using System.Text;

namespace LuafuscatorDeobf
{
    public enum TK
    {
        Name, Number, String, True, False, Nil,
        And, Break, Do, Else, Elseif, End, For, Function,
        Goto, If, In, Local, Not, Or, Repeat, Return,
        Then, Until, While,
        Plus, Minus, Star, Slash, Percent, Caret, Hash,
        Ampersand, Tilde, Pipe, ShiftL, ShiftR, SlashSlash,
        Eq, NotEq, Lt, Gt, LtEq, GtEq,
        Assign,
        LParen, RParen, LBrace, RBrace, LBracket, RBracket,
        Semicolon, Colon, ColonColon, Comma, Dot, DotDot, DotDotDot,
        EOF
    }

    public class Token
    {
        public TK    Kind;
        public string Raw;
        public int   Line;

        public Token(TK kind, string raw, int line)
        {
            Kind = kind;
            Raw  = raw;
            Line = line;
        }
    }

    public class LuaLexer
    {
        private readonly string _src;
        private int _pos;
        private int _line = 1;

        private static readonly Dictionary<string, TK> Keywords = new Dictionary<string, TK>
        {
            ["and"]      = TK.And,
            ["break"]    = TK.Break,
            ["do"]       = TK.Do,
            ["else"]     = TK.Else,
            ["elseif"]   = TK.Elseif,
            ["end"]      = TK.End,
            ["false"]    = TK.False,
            ["for"]      = TK.For,
            ["function"] = TK.Function,
            ["goto"]     = TK.Goto,
            ["if"]       = TK.If,
            ["in"]       = TK.In,
            ["local"]    = TK.Local,
            ["nil"]      = TK.Nil,
            ["not"]      = TK.Not,
            ["or"]       = TK.Or,
            ["repeat"]   = TK.Repeat,
            ["return"]   = TK.Return,
            ["then"]     = TK.Then,
            ["true"]     = TK.True,
            ["until"]    = TK.Until,
            ["while"]    = TK.While,
        };

        public LuaLexer(string src)
        {
            _src = src;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (true)
            {
                var tok = Next();
                tokens.Add(tok);
                if (tok.Kind == TK.EOF) break;
            }
            return tokens;
        }

        private char Cur  => _pos < _src.Length ? _src[_pos]         : '\0';
        private char Peek => _pos + 1 < _src.Length ? _src[_pos + 1] : '\0';

        private void Advance()
        {
            if (_pos < _src.Length && _src[_pos] == '\n') _line++;
            _pos++;
        }

        private Token Next()
        {
            while (_pos < _src.Length)
            {
                if (Cur == '\n' || Cur == '\r' || Cur == ' ' || Cur == '\t')
                {
                    Advance();
                    continue;
                }

                if (Cur == '-' && Peek == '-')
                {
                    _pos += 2;
                    if (Cur == '[')
                    {
                        int level = CountLongBracket();
                        if (level >= 0)
                        {
                            SkipLongString(level);
                            continue;
                        }
                    }
                    while (_pos < _src.Length && Cur != '\n') _pos++;
                    continue;
                }

                break;
            }

            if (_pos >= _src.Length) return new Token(TK.EOF, "", _line);

            int  startLine = _line;
            char c         = Cur;

            if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek)))
                return ReadNumber(startLine);

            if (c == '"' || c == '\'')
                return ReadShortString(startLine);

            if (c == '[')
            {
                int lv = CountLongBracket();
                if (lv >= 0) return ReadLongString(startLine, lv);
            }

            if (char.IsLetter(c) || c == '_')
                return ReadName(startLine);

            return ReadPunct(startLine);
        }

        private int CountLongBracket()
        {
            if (Cur != '[') return -1;
            int save  = _pos;
            int level = 0;
            _pos++;
            while (_pos < _src.Length && _src[_pos] == '=') { level++; _pos++; }
            if (_pos < _src.Length && _src[_pos] == '[') { _pos = save; return level; }
            _pos = save;
            return -1;
        }

        private void SkipLongString(int level)
        {
            _pos++;
            for (int i = 0; i < level; i++) _pos++;
            _pos++;

            while (_pos < _src.Length)
            {
                if (Cur == '\n') _line++;
                if (Cur == ']')
                {
                    _pos++;
                    int eq = 0;
                    while (_pos < _src.Length && _src[_pos] == '=') { eq++; _pos++; }
                    if (eq == level && _pos < _src.Length && _src[_pos] == ']') { _pos++; return; }
                }
                else _pos++;
            }
        }

        private Token ReadLongString(int line, int level)
        {
            var sb = new StringBuilder();
            sb.Append(_src[_pos]); _pos++;
            for (int i = 0; i < level; i++) { sb.Append(_src[_pos]); _pos++; }
            sb.Append(_src[_pos]); _pos++;

            while (_pos < _src.Length)
            {
                if (Cur == '\n') _line++;
                if (Cur == ']')
                {
                    sb.Append(_src[_pos]); _pos++;
                    int eq = 0;
                    while (_pos < _src.Length && _src[_pos] == '=') { sb.Append(_src[_pos]); eq++; _pos++; }
                    if (eq == level && _pos < _src.Length && _src[_pos] == ']')
                    {
                        sb.Append(_src[_pos]); _pos++;
                        return new Token(TK.String, sb.ToString(), line);
                    }
                }
                else { sb.Append(_src[_pos]); _pos++; }
            }

            return new Token(TK.String, sb.ToString(), line);
        }

        private Token ReadShortString(int line)
        {
            var  sb = new StringBuilder();
            char q  = Cur;
            sb.Append(q);
            Advance();

            while (_pos < _src.Length && Cur != q)
            {
                if (Cur == '\n') break;
                if (Cur == '\\')
                {
                    sb.Append(Cur);
                    Advance();
                    if (_pos < _src.Length) { sb.Append(Cur); Advance(); }
                    continue;
                }
                sb.Append(Cur);
                Advance();
            }

            if (_pos < _src.Length) { sb.Append(Cur); Advance(); }
            return new Token(TK.String, sb.ToString(), line);
        }

        private Token ReadNumber(int line)
        {
            var sb = new StringBuilder();

            if (Cur == '0' && (Peek == 'x' || Peek == 'X'))
            {
                sb.Append(Cur); Advance();
                sb.Append(Cur); Advance();
                while (_pos < _src.Length && (IsHex(Cur) || Cur == '_')) { sb.Append(Cur); Advance(); }
                return new Token(TK.Number, sb.ToString(), line);
            }

            while (_pos < _src.Length && (char.IsDigit(Cur) || Cur == '.' || Cur == '_'))
            {
                sb.Append(Cur);
                Advance();
            }

            if (_pos < _src.Length && (Cur == 'e' || Cur == 'E'))
            {
                sb.Append(Cur); Advance();
                if (_pos < _src.Length && (Cur == '+' || Cur == '-')) { sb.Append(Cur); Advance(); }
                while (_pos < _src.Length && char.IsDigit(Cur)) { sb.Append(Cur); Advance(); }
            }

            return new Token(TK.Number, sb.ToString(), line);
        }

        private Token ReadName(int line)
        {
            var sb = new StringBuilder();
            while (_pos < _src.Length && (char.IsLetterOrDigit(Cur) || Cur == '_'))
            {
                sb.Append(Cur);
                Advance();
            }
            string s = sb.ToString();
            return Keywords.TryGetValue(s, out TK kw) ? new Token(kw, s, line) : new Token(TK.Name, s, line);
        }

        private Token ReadPunct(int line)
        {
            char c = Cur;
            Advance();

            switch (c)
            {
                case '+': return new Token(TK.Plus,      "+",  line);
                case '*': return new Token(TK.Star,      "*",  line);
                case '^': return new Token(TK.Caret,     "^",  line);
                case '#': return new Token(TK.Hash,      "#",  line);
                case '&': return new Token(TK.Ampersand, "&",  line);
                case '|': return new Token(TK.Pipe,      "|",  line);
                case '(': return new Token(TK.LParen,    "(",  line);
                case ')': return new Token(TK.RParen,    ")",  line);
                case '{': return new Token(TK.LBrace,    "{",  line);
                case '}': return new Token(TK.RBrace,    "}",  line);
                case '[': return new Token(TK.LBracket,  "[",  line);
                case ']': return new Token(TK.RBracket,  "]",  line);
                case ';': return new Token(TK.Semicolon, ";",  line);
                case ',': return new Token(TK.Comma,     ",",  line);
                case '%': return new Token(TK.Percent,   "%",  line);
                case '-': return new Token(TK.Minus,     "-",  line);
                case '/':
                    if (Cur == '/') { Advance(); return new Token(TK.SlashSlash, "//", line); }
                    return new Token(TK.Slash, "/", line);
                case '~':
                    if (Cur == '=') { Advance(); return new Token(TK.NotEq, "~=", line); }
                    return new Token(TK.Tilde, "~", line);
                case '<':
                    if (Cur == '<') { Advance(); return new Token(TK.ShiftL, "<<",  line); }
                    if (Cur == '=') { Advance(); return new Token(TK.LtEq,   "<=",  line); }
                    return new Token(TK.Lt, "<", line);
                case '>':
                    if (Cur == '>') { Advance(); return new Token(TK.ShiftR, ">>",  line); }
                    if (Cur == '=') { Advance(); return new Token(TK.GtEq,   ">=",  line); }
                    return new Token(TK.Gt, ">", line);
                case '=':
                    if (Cur == '=') { Advance(); return new Token(TK.Eq, "==", line); }
                    return new Token(TK.Assign, "=", line);
                case ':':
                    if (Cur == ':') { Advance(); return new Token(TK.ColonColon, "::", line); }
                    return new Token(TK.Colon, ":", line);
                case '.':
                    if (Cur == '.')
                    {
                        Advance();
                        if (Cur == '.') { Advance(); return new Token(TK.DotDotDot, "...", line); }
                        return new Token(TK.DotDot, "..", line);
                    }
                    return new Token(TK.Dot, ".", line);
                default:
                    return new Token(TK.Name, c.ToString(), line);
            }
        }

        private static bool IsHex(char c) =>
            char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
