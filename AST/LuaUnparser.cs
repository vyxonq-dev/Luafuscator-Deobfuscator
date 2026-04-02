using _1._0._8._0_D.ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    public class LuaUnparser
    {
        private readonly Dictionary<string, string> _renames;
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indent;

        public LuaUnparser(Dictionary<string, string> renames)
        {
            _renames = renames ?? new Dictionary<string, string>();
        }

        public string Emit(BlockNode root)
        {
            EmitBlock(root);
            return _sb.ToString();
        }

        private void Write(string s) => _sb.Append(s);
        private void NL()            => _sb.Append('\n');

        private void Indent()
        {
            for (int i = 0; i < _indent; i++) _sb.Append("    ");
        }

        private void Line(string s)
        {
            Indent();
            _sb.Append(s);
            NL();
        }

        private string R(string name) =>
            _renames.TryGetValue(name, out string v) ? v : name;

        private void EmitBlock(BlockNode block)
        {
            foreach (var stmt in block.Stmts)
                EmitStmt(stmt);
        }

        private void EmitStmt(LuaNode stmt)
        {
            switch (stmt)
            {
                case LocalNode loc:
                    Indent();
                    Write("local ");
                    for (int i = 0; i < loc.Names.Count; i++)
                    {
                        if (i > 0) Write(", ");
                        Write(R(loc.Names[i]));
                        if (loc.Attrs != null && i < loc.Attrs.Count && loc.Attrs[i] != null)
                            Write($" <{loc.Attrs[i]}>");
                    }
                    if (loc.Values != null && loc.Values.Count > 0)
                    {
                        Write(" = ");
                        EmitExprList(loc.Values);
                    }
                    NL();
                    break;

                case LocalFuncNode lf:
                    Indent();
                    Write($"local function {R(lf.Name)}");
                    EmitFuncBody(lf.Func);
                    NL();
                    break;

                case FuncStmtNode fs:
                    Indent();
                    Write("function ");
                    Write(string.Join(".", fs.Names));
                    if (fs.MethodName != null) Write(":" + fs.MethodName);
                    EmitFuncBody(fs.Func);
                    NL();
                    break;

                case AssignNode a:
                    Indent();
                    EmitExprList(a.Targets);
                    Write(" = ");
                    EmitExprList(a.Values);
                    NL();
                    break;

                case CallStmtNode cs:
                    Indent();
                    EmitExpr(cs.Call);
                    NL();
                    break;

                case DoNode d:
                    Line("do");
                    _indent++;
                    EmitBlock(d.Body);
                    _indent--;
                    Line("end");
                    break;

                case WhileNode w:
                    Indent();
                    Write("while ");
                    EmitExpr(w.Cond);
                    Write(" do");
                    NL();
                    _indent++;
                    EmitBlock(w.Body);
                    _indent--;
                    Line("end");
                    break;

                case RepeatNode r:
                    Line("repeat");
                    _indent++;
                    EmitBlock(r.Body);
                    _indent--;
                    Indent();
                    Write("until ");
                    EmitExpr(r.Cond);
                    NL();
                    break;

                case IfNode ifn:
                    for (int i = 0; i < ifn.Clauses.Count; i++)
                    {
                        var (c, b) = ifn.Clauses[i];
                        Indent();
                        Write(i == 0 ? "if " : "elseif ");
                        EmitExpr(c);
                        Write(" then");
                        NL();
                        _indent++;
                        EmitBlock(b);
                        _indent--;
                    }
                    if (ifn.Else != null)
                    {
                        Line("else");
                        _indent++;
                        EmitBlock(ifn.Else);
                        _indent--;
                    }
                    Line("end");
                    break;

                case ForNumNode fn:
                    Indent();
                    Write($"for {R(fn.Var)} = ");
                    EmitExpr(fn.Start);
                    Write(", ");
                    EmitExpr(fn.Limit);
                    if (fn.Step != null) { Write(", "); EmitExpr(fn.Step); }
                    Write(" do");
                    NL();
                    _indent++;
                    EmitBlock(fn.Body);
                    _indent--;
                    Line("end");
                    break;

                case ForInNode fi:
                    Indent();
                    Write("for ");
                    Write(string.Join(", ", fi.Vars.ConvertAll(R)));
                    Write(" in ");
                    EmitExprList(fi.Iters);
                    Write(" do");
                    NL();
                    _indent++;
                    EmitBlock(fi.Body);
                    _indent--;
                    Line("end");
                    break;

                case ReturnNode ret:
                    Indent();
                    Write("return");
                    if (ret.Values != null && ret.Values.Count > 0)
                    {
                        Write(" ");
                        EmitExprList(ret.Values);
                    }
                    NL();
                    break;

                case BreakNode:
                    Line("break");
                    break;

                case GotoNode g:
                    Line($"goto {g.Label}");
                    break;

                case LabelNode l:
                    Line($"::{l.Name}::");
                    break;

                default:
                    Line($"-- [unparser: unhandled {stmt.GetType().Name}]");
                    break;
            }
        }

        private void EmitFuncBody(FuncExpr fe)
        {
            Write("(");
            for (int i = 0; i < fe.Params.Count; i++)
            {
                if (i > 0) Write(", ");
                Write(R(fe.Params[i]));
            }
            if (fe.HasVarArg)
            {
                if (fe.Params.Count > 0) Write(", ");
                Write("...");
            }
            Write(")");
            NL();
            _indent++;
            EmitBlock(fe.Body);
            _indent--;
            Indent();
            Write("end");
        }

        private void EmitExprList(List<ExprNode> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) Write(", ");
                EmitExpr(list[i]);
            }
        }

        private void EmitExpr(ExprNode expr, int parentPrec = 0)
        {
            switch (expr)
            {
                case NilExpr:    Write("nil");   break;
                case TrueExpr:   Write("true");  break;
                case FalseExpr:  Write("false"); break;
                case VarArgExpr: Write("...");   break;
                case NumberExpr n: Write(n.Raw); break;
                case StringExpr s: Write(s.Raw); break;

                case NameExpr ne:
                    Write(R(ne.Name));
                    break;

                case UnopExpr u:
                    Write(u.Op == "not" ? "not " : u.Op);
                    bool needParen = u.Operand is BinopExpr;
                    if (needParen) Write("(");
                    EmitExpr(u.Operand, 11);
                    if (needParen) Write(")");
                    break;

                case BinopExpr b:
                    int  prec = BinopPrec(b.Op);
                    bool lp   = b.Left  is BinopExpr lb && BinopPrec(lb.Op) < prec;
                    bool rp   = b.Right is BinopExpr rb && (b.Op == ".." || b.Op == "^"
                        ? BinopPrec(rb.Op) < prec
                        : BinopPrec(rb.Op) <= prec);
                    if (lp) Write("(");
                    EmitExpr(b.Left, prec);
                    if (lp) Write(")");
                    Write($" {b.Op} ");
                    if (rp) Write("(");
                    EmitExpr(b.Right, prec);
                    if (rp) Write(")");
                    break;

                case FieldExpr fe:
                    EmitPrefixed(fe.Table);
                    Write(".");
                    Write(fe.Field);
                    break;

                case IndexExpr ie:
                    EmitPrefixed(ie.Table);
                    Write("[");
                    EmitExpr(ie.Key);
                    Write("]");
                    break;

                case CallExpr ce:
                    EmitPrefixed(ce.Func);
                    Write("(");
                    EmitExprList(ce.Args);
                    Write(")");
                    break;

                case MethodCallExpr mc:
                    EmitPrefixed(mc.Obj);
                    Write(":");
                    Write(mc.Method);
                    Write("(");
                    EmitExprList(mc.Args);
                    Write(")");
                    break;

                case FuncExpr fe2:
                    Write("function");
                    EmitFuncBody(fe2);
                    break;

                case TableExpr te:
                    EmitTable(te);
                    break;

                default:
                    Write($"--[[?{expr.GetType().Name}]]");
                    break;
            }
        }

        private void EmitPrefixed(ExprNode e)
        {
            bool wrap = e is BinopExpr || e is UnopExpr || e is FuncExpr;
            if (wrap) Write("(");
            EmitExpr(e);
            if (wrap) Write(")");
        }

        private void EmitTable(TableExpr te)
        {
            if (te.Fields.Count == 0) { Write("{}"); return; }

            bool inline = te.Fields.Count <= 4 && te.Fields.TrueForAll(f => f.Key == null);

            if (inline)
            {
                Write("{");
                for (int i = 0; i < te.Fields.Count; i++)
                {
                    if (i > 0) Write(", ");
                    EmitExpr(te.Fields[i].Value);
                }
                Write("}");
                return;
            }

            Write("{");
            NL();
            _indent++;
            foreach (var f in te.Fields)
            {
                Indent();
                if (f.Key != null)
                {
                    if (f.Key is StringExpr sk && IsIdent(UnquoteRaw(sk.Raw)))
                    {
                        Write(UnquoteRaw(sk.Raw));
                        Write(" = ");
                    }
                    else
                    {
                        Write("[");
                        EmitExpr(f.Key);
                        Write("] = ");
                    }
                }
                EmitExpr(f.Value);
                Write(",");
                NL();
            }
            _indent--;
            Indent();
            Write("}");
        }

        private static int BinopPrec(string op) => op switch
        {
            "or"  => 1,
            "and" => 2,
            "<" or ">" or "<=" or ">=" or "==" or "~=" => 3,
            "|"   => 4,
            "~"   => 5,
            "&"   => 6,
            "<<" or ">>" => 7,
            ".."  => 8,
            "+" or "-" => 9,
            "*" or "/" or "//" or "%" => 10,
            "^"   => 12,
            _     => 0
        };

        private static string UnquoteRaw(string raw)
        {
            if (raw.Length >= 2 && (raw[0] == '"' || raw[0] == '\''))
                return raw.Substring(1, raw.Length - 2);
            return raw;
        }

        private static bool IsIdent(string s) =>
            s.Length > 0 &&
            (char.IsLetter(s[0]) || s[0] == '_') &&
            Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_]*$");
    }
}
