using _1._0._8._0_D.ast;
using System;
using System.Collections.Generic;

namespace LuafuscatorDeobf
{
    public class LuaParser
    {
        private readonly List<Token> _toks;
        private int _pos;

        public List<string> Errors { get; } = new List<string>();

        public LuaParser(List<Token> tokens)
        {
            _toks = tokens;
        }

        private Token Cur  => _toks[Math.Min(_pos,     _toks.Count - 1)];
        private Token Peek => _toks[Math.Min(_pos + 1, _toks.Count - 1)];

        private Token Consume()
        {
            var t = Cur;
            if (_pos < _toks.Count - 1) _pos++;
            return t;
        }

        private Token Expect(TK kind)
        {
            if (Cur.Kind == kind) return Consume();
            Errors.Add($"L{Cur.Line}: expected {kind}, got {Cur.Kind} `{Cur.Raw}`");
            return new Token(kind, "", Cur.Line);
        }

        private bool Check(TK k) => Cur.Kind == k;

        private bool Match(TK k)
        {
            if (Check(k)) { Consume(); return true; }
            return false;
        }

        public BlockNode ParseBlock()
        {
            var block = new BlockNode { Line = Cur.Line };

            while (true)
            {
                SkipSemicolons();
                if (IsBlockEnd()) break;

                var stmt = ParseStmt();
                if (stmt == null) break;
                block.Stmts.Add(stmt);

                if (stmt is ReturnNode) { SkipSemicolons(); break; }
            }

            return block;
        }

        private bool IsBlockEnd()
        {
            var k = Cur.Kind;
            return k == TK.EOF   ||
                   k == TK.End   ||
                   k == TK.Else  ||
                   k == TK.Elseif ||
                   k == TK.Until;
        }

        private void SkipSemicolons()
        {
            while (Match(TK.Semicolon)) { }
        }

        private LuaNode ParseStmt()
        {
            int line = Cur.Line;

            switch (Cur.Kind)
            {
                case TK.If:       return ParseIf();
                case TK.While:    return ParseWhile();
                case TK.Do:       return ParseDo();
                case TK.For:      return ParseFor();
                case TK.Repeat:   return ParseRepeat();
                case TK.Function: return ParseFuncStmt();
                case TK.Local:    return ParseLocal();
                case TK.Return:   return ParseReturn();
                case TK.Break:
                    Consume();
                    return new BreakNode { Line = line };
                case TK.Goto:
                    Consume();
                    return new GotoNode { Line = line, Label = Expect(TK.Name).Raw };
                case TK.ColonColon:
                    Consume();
                    string lbl = Expect(TK.Name).Raw;
                    Expect(TK.ColonColon);
                    return new LabelNode { Line = line, Name = lbl };
                default:
                    return ParseExprStat();
            }
        }

        private IfNode ParseIf()
        {
            int  line = Cur.Line;
            Consume();
            var node = new IfNode { Line = line };
            var cond = ParseExpr();
            Expect(TK.Then);
            var body = ParseBlock();
            node.Clauses.Add((cond, body));

            while (Check(TK.Elseif))
            {
                Consume();
                var ec = ParseExpr();
                Expect(TK.Then);
                var eb = ParseBlock();
                node.Clauses.Add((ec, eb));
            }

            if (Match(TK.Else)) node.Else = ParseBlock();
            Expect(TK.End);
            return node;
        }

        private WhileNode ParseWhile()
        {
            int line = Cur.Line;
            Consume();
            var cond = ParseExpr();
            Expect(TK.Do);
            var body = ParseBlock();
            Expect(TK.End);
            return new WhileNode { Line = line, Cond = cond, Body = body };
        }

        private DoNode ParseDo()
        {
            int line = Cur.Line;
            Consume();
            var body = ParseBlock();
            Expect(TK.End);
            return new DoNode { Line = line, Body = body };
        }

        private LuaNode ParseFor()
        {
            int    line      = Cur.Line;
            Consume();
            string firstName = Expect(TK.Name).Raw;

            if (Match(TK.Assign))
            {
                var start = ParseExpr();
                Expect(TK.Comma);
                var limit = ParseExpr();
                ExprNode step = null;
                if (Match(TK.Comma)) step = ParseExpr();
                Expect(TK.Do);
                var body = ParseBlock();
                Expect(TK.End);
                return new ForNumNode
                {
                    Line  = line,
                    Var   = firstName,
                    Start = start,
                    Limit = limit,
                    Step  = step,
                    Body  = body
                };
            }
            else
            {
                var vars = new List<string> { firstName };
                while (Match(TK.Comma)) vars.Add(Expect(TK.Name).Raw);
                Expect(TK.In);
                var iters = ParseExprList();
                Expect(TK.Do);
                var body = ParseBlock();
                Expect(TK.End);
                return new ForInNode { Line = line, Vars = vars, Iters = iters, Body = body };
            }
        }

        private RepeatNode ParseRepeat()
        {
            int line = Cur.Line;
            Consume();
            var body = ParseBlock();
            Expect(TK.Until);
            var cond = ParseExpr();
            return new RepeatNode { Line = line, Body = body, Cond = cond };
        }

        private LuaNode ParseFuncStmt()
        {
            int line = Cur.Line;
            Consume();
            var names = new List<string> { Expect(TK.Name).Raw };
            while (Match(TK.Dot)) names.Add(Expect(TK.Name).Raw);
            string method = null;
            if (Match(TK.Colon)) method = Expect(TK.Name).Raw;
            var func = ParseFuncBody(line, method != null);
            return new FuncStmtNode { Line = line, Names = names, MethodName = method, Func = func };
        }

        private LuaNode ParseLocal()
        {
            int line = Cur.Line;
            Consume();

            if (Check(TK.Function))
            {
                Consume();
                string name = Expect(TK.Name).Raw;
                var    func = ParseFuncBody(line, false);
                return new LocalFuncNode { Line = line, Name = name, Func = func };
            }

            var names = new List<string>();
            var attrs = new List<string>();
            names.Add(Expect(TK.Name).Raw);
            attrs.Add(ParseAttrib());

            while (Match(TK.Comma))
            {
                names.Add(Expect(TK.Name).Raw);
                attrs.Add(ParseAttrib());
            }

            var vals = new List<ExprNode>();
            if (Match(TK.Assign)) vals = ParseExprList();

            return new LocalNode { Line = line, Names = names, Attrs = attrs, Values = vals };
        }

        private string ParseAttrib()
        {
            if (Match(TK.Lt))
            {
                string a = Expect(TK.Name).Raw;
                Expect(TK.Gt);
                return a;
            }
            return null;
        }

        private ReturnNode ParseReturn()
        {
            int line = Cur.Line;
            Consume();
            var vals = new List<ExprNode>();
            if (!IsBlockEnd() && !Check(TK.Semicolon))
                vals = ParseExprList();
            Match(TK.Semicolon);
            return new ReturnNode { Line = line, Values = vals };
        }

        private LuaNode ParseExprStat()
        {
            int  line = Cur.Line;
            var  expr = ParseSuffixedExpr();

            if (Check(TK.Assign) || Check(TK.Comma))
            {
                var targets = new List<ExprNode> { expr };
                while (Match(TK.Comma)) targets.Add(ParseSuffixedExpr());
                Expect(TK.Assign);
                var vals = ParseExprList();
                return new AssignNode { Line = line, Targets = targets, Values = vals };
            }

            if (expr is CallExpr || expr is MethodCallExpr)
                return new CallStmtNode { Line = line, Call = expr };

            Errors.Add($"L{line}: unexpected expression statement `{Cur.Raw}`");
            while (!IsBlockEnd() && !Check(TK.EOF) && !Check(TK.Semicolon) && !IsStmtStart())
                Consume();

            return new CallStmtNode { Line = line, Call = expr };
        }

        private bool IsStmtStart()
        {
            var k = Cur.Kind;
            return k == TK.If       ||
                   k == TK.While    ||
                   k == TK.Do       ||
                   k == TK.For      ||
                   k == TK.Repeat   ||
                   k == TK.Function ||
                   k == TK.Local    ||
                   k == TK.Return   ||
                   k == TK.Break    ||
                   k == TK.Goto;
        }

        private FuncExpr ParseFuncBody(int line, bool hasImplicitSelf)
        {
            Expect(TK.LParen);
            var  parms  = new List<string>();
            bool vararg = false;

            if (hasImplicitSelf) parms.Add("self");

            if (!Check(TK.RParen))
            {
                if (Check(TK.DotDotDot))
                {
                    Consume();
                    vararg = true;
                }
                else
                {
                    parms.Add(Expect(TK.Name).Raw);
                    while (Match(TK.Comma))
                    {
                        if (Check(TK.DotDotDot)) { Consume(); vararg = true; break; }
                        parms.Add(Expect(TK.Name).Raw);
                    }
                }
            }

            Expect(TK.RParen);
            var body = ParseBlock();
            Expect(TK.End);
            return new FuncExpr { Line = line, Params = parms, HasVarArg = vararg, Body = body };
        }

        private List<ExprNode> ParseExprList()
        {
            var list = new List<ExprNode> { ParseExpr() };
            while (Match(TK.Comma)) list.Add(ParseExpr());
            return list;
        }

        private ExprNode ParseExpr() => ParseBinop(0);

        private static readonly Dictionary<TK, (int L, int R)> BinopPriority =
            new Dictionary<TK, (int, int)>
        {
            [TK.Or]        = (1,  1),
            [TK.And]       = (2,  2),
            [TK.Lt]        = (3,  3),
            [TK.Gt]        = (3,  3),
            [TK.LtEq]      = (3,  3),
            [TK.GtEq]      = (3,  3),
            [TK.Eq]        = (3,  3),
            [TK.NotEq]     = (3,  3),
            [TK.Pipe]      = (4,  4),
            [TK.Tilde]     = (5,  5),
            [TK.Ampersand] = (6,  6),
            [TK.ShiftL]    = (7,  7),
            [TK.ShiftR]    = (7,  7),
            [TK.DotDot]    = (8,  7),
            [TK.Plus]      = (9,  9),
            [TK.Minus]     = (9,  9),
            [TK.Star]      = (10, 10),
            [TK.Slash]     = (10, 10),
            [TK.SlashSlash]= (10, 10),
            [TK.Percent]   = (10, 10),
            [TK.Caret]     = (12, 11),
        };

        private static string BinopStr(TK k) => k switch
        {
            TK.Or         => "or",
            TK.And        => "and",
            TK.Lt         => "<",
            TK.Gt         => ">",
            TK.LtEq       => "<=",
            TK.GtEq       => ">=",
            TK.Eq         => "==",
            TK.NotEq      => "~=",
            TK.Plus       => "+",
            TK.Minus      => "-",
            TK.Star       => "*",
            TK.Slash      => "/",
            TK.SlashSlash => "//",
            TK.Percent    => "%",
            TK.Caret      => "^",
            TK.DotDot     => "..",
            TK.Ampersand  => "&",
            TK.Pipe       => "|",
            TK.Tilde      => "~",
            TK.ShiftL     => "<<",
            TK.ShiftR     => ">>",
            _             => "?"
        };

        private ExprNode ParseBinop(int minPrec)
        {
            var left = ParseUnop();

            while (BinopPriority.TryGetValue(Cur.Kind, out var pri) && pri.L > minPrec)
            {
                string op   = BinopStr(Cur.Kind);
                int    line = Cur.Line;
                Consume();
                var right = ParseBinop(pri.R);
                left = new BinopExpr { Line = line, Op = op, Left = left, Right = right };
            }

            return left;
        }

        private ExprNode ParseUnop()
        {
            int line = Cur.Line;

            if (Match(TK.Minus)) return new UnopExpr { Line = line, Op = "-",   Operand = ParseBinop(11) };
            if (Match(TK.Not))   return new UnopExpr { Line = line, Op = "not", Operand = ParseBinop(11) };
            if (Match(TK.Hash))  return new UnopExpr { Line = line, Op = "#",   Operand = ParseBinop(11) };
            if (Match(TK.Tilde)) return new UnopExpr { Line = line, Op = "~",   Operand = ParseBinop(11) };

            return ParseSuffixedExpr();
        }

        private ExprNode ParseSuffixedExpr()
        {
            var expr = ParsePrimaryExpr();

            while (true)
            {
                int line = Cur.Line;

                if (Match(TK.Dot))
                {
                    string field = Expect(TK.Name).Raw;
                    expr = new FieldExpr { Line = line, Table = expr, Field = field };
                }
                else if (Match(TK.LBracket))
                {
                    var key = ParseExpr();
                    Expect(TK.RBracket);
                    expr = new IndexExpr { Line = line, Table = expr, Key = key };
                }
                else if (Match(TK.Colon))
                {
                    string method = Expect(TK.Name).Raw;
                    var    args   = ParseCallArgs(line);
                    expr = new MethodCallExpr { Line = line, Obj = expr, Method = method, Args = args };
                }
                else if (Check(TK.LParen) || Check(TK.LBrace) || Check(TK.String))
                {
                    var args = ParseCallArgs(line);
                    expr = new CallExpr { Line = line, Func = expr, Args = args };
                }
                else break;
            }

            return expr;
        }

        private List<ExprNode> ParseCallArgs(int line)
        {
            if (Match(TK.LParen))
            {
                if (Check(TK.RParen)) { Consume(); return new List<ExprNode>(); }
                var a = ParseExprList();
                Expect(TK.RParen);
                return a;
            }

            if (Check(TK.LBrace))
                return new List<ExprNode> { ParseTableCtor() };

            if (Check(TK.String))
                return new List<ExprNode> { new StringExpr { Line = line, Raw = Consume().Raw } };

            Errors.Add($"L{line}: expected call args");
            return new List<ExprNode>();
        }

        private ExprNode ParsePrimaryExpr()
        {
            int line = Cur.Line;

            if (Check(TK.Name))
                return new NameExpr { Line = line, Name = Consume().Raw };

            if (Match(TK.LParen))
            {
                var e = ParseExpr();
                Expect(TK.RParen);
                return e;
            }

            return ParseSimpleExpr();
        }

        private ExprNode ParseSimpleExpr()
        {
            int line = Cur.Line;

            switch (Cur.Kind)
            {
                case TK.Number:    return new NumberExpr { Line = line, Raw = Consume().Raw };
                case TK.String:    return new StringExpr { Line = line, Raw = Consume().Raw };
                case TK.True:      Consume(); return new TrueExpr   { Line = line };
                case TK.False:     Consume(); return new FalseExpr  { Line = line };
                case TK.Nil:       Consume(); return new NilExpr    { Line = line };
                case TK.DotDotDot: Consume(); return new VarArgExpr { Line = line };
                case TK.Function:  Consume(); return ParseFuncBody(line, false);
                case TK.LBrace:    return ParseTableCtor();
                default:
                    Errors.Add($"L{line}: unexpected token {Cur.Kind} `{Cur.Raw}` in expression");
                    return new NilExpr { Line = line };
            }
        }

        private TableExpr ParseTableCtor()
        {
            int line = Cur.Line;
            Expect(TK.LBrace);
            var fields = new List<TableField>();

            while (!Check(TK.RBrace) && !Check(TK.EOF))
            {
                var f = new TableField { Line = Cur.Line };

                if (Check(TK.LBracket))
                {
                    Consume();
                    f.Key = ParseExpr();
                    Expect(TK.RBracket);
                    Expect(TK.Assign);
                    f.Value = ParseExpr();
                }
                else if (Check(TK.Name) && Peek.Kind == TK.Assign)
                {
                    f.Key   = new StringExpr { Line = Cur.Line, Raw = "\"" + Cur.Raw + "\"" };
                    Consume();
                    Consume();
                    f.Value = ParseExpr();
                }
                else
                {
                    f.Value = ParseExpr();
                }

                fields.Add(f);
                if (!Match(TK.Comma) && !Match(TK.Semicolon)) break;
            }

            Expect(TK.RBrace);
            return new TableExpr { Line = line, Fields = fields };
        }
    }
}
