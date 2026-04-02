using _1._0._8._0_D.ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    public class LuaRenamer
    {
        private static readonly Regex ObfPattern = new Regex(
            @"^_[lI1O0][lI1O0A-Za-z0-9_]{4,}$",
            RegexOptions.Compiled);

        private int _varCnt;
        private int _fnCnt;
        private int _paramCnt;

        public Dictionary<string, string> Renames        { get; } = new Dictionary<string, string>();
        public Dictionary<string, long>   KnownConstants { get; set; }

        public void Analyse(BlockNode root)
        {
            var scope = new Scope(null);
            AnalyseBlock(root, scope);
        }

        private void AnalyseBlock(BlockNode block, Scope scope)
        {
            foreach (var stmt in block.Stmts)
                AnalyseStmt(stmt, scope);
        }

        private void AnalyseStmt(LuaNode stmt, Scope scope)
        {
            switch (stmt)
            {
                case LocalNode loc:
                    foreach (var v in loc.Values) AnalyseExpr(v, scope);
                    for (int i = 0; i < loc.Names.Count; i++)
                    {
                        string   orig = loc.Names[i];
                        ExprNode rhs  = i < loc.Values.Count ? loc.Values[i] : null;

                        if (IsObf(orig))
                        {
                            string newName = GenerateName(orig, rhs);
                            scope.Declare(orig, newName);
                            RegisterRename(orig, newName);
                        }
                        else
                        {
                            scope.Declare(orig, orig);
                        }
                    }
                    break;

                case LocalFuncNode lf:
                    string fnNew = IsObf(lf.Name) ? NewFn() : lf.Name;
                    scope.Declare(lf.Name, fnNew);
                    RegisterRename(lf.Name, fnNew);
                    var lfScope = new Scope(scope);
                    AnalyseFuncParams(lf.Func, lfScope);
                    AnalyseBlock(lf.Func.Body, lfScope);
                    break;

                case FuncStmtNode fs:
                    var fsScope = new Scope(scope);
                    AnalyseFuncParams(fs.Func, fsScope);
                    AnalyseBlock(fs.Func.Body, fsScope);
                    break;

                case AssignNode a:
                    foreach (var v in a.Values)  AnalyseExpr(v, scope);
                    foreach (var t in a.Targets)  AnalyseExpr(t, scope);
                    break;

                case DoNode d:
                    AnalyseBlock(d.Body, new Scope(scope));
                    break;

                case WhileNode w:
                    AnalyseExpr(w.Cond, scope);
                    AnalyseBlock(w.Body, new Scope(scope));
                    break;

                case RepeatNode r:
                    var rs = new Scope(scope);
                    AnalyseBlock(r.Body, rs);
                    AnalyseExpr(r.Cond, rs);
                    break;

                case IfNode ifn:
                    foreach (var (c, b) in ifn.Clauses)
                    {
                        AnalyseExpr(c, scope);
                        AnalyseBlock(b, new Scope(scope));
                    }
                    if (ifn.Else != null) AnalyseBlock(ifn.Else, new Scope(scope));
                    break;

                case ForNumNode fn:
                    AnalyseExpr(fn.Start, scope);
                    AnalyseExpr(fn.Limit, scope);
                    if (fn.Step != null) AnalyseExpr(fn.Step, scope);
                    var fns = new Scope(scope);
                    if (IsObf(fn.Var))
                    {
                        string nn = NewVar();
                        fns.Declare(fn.Var, nn);
                        RegisterRename(fn.Var, nn);
                    }
                    else fns.Declare(fn.Var, fn.Var);
                    AnalyseBlock(fn.Body, fns);
                    break;

                case ForInNode fi:
                    foreach (var it in fi.Iters) AnalyseExpr(it, scope);
                    var fis = new Scope(scope);
                    foreach (var v in fi.Vars)
                    {
                        if (IsObf(v))
                        {
                            string nn = NewVar();
                            fis.Declare(v, nn);
                            RegisterRename(v, nn);
                        }
                        else fis.Declare(v, v);
                    }
                    AnalyseBlock(fi.Body, fis);
                    break;

                case ReturnNode ret:
                    foreach (var v in ret.Values) AnalyseExpr(v, scope);
                    break;

                case CallStmtNode cs:
                    AnalyseExpr(cs.Call, scope);
                    break;
            }
        }

        private void AnalyseFuncParams(FuncExpr fe, Scope scope)
        {
            foreach (var p in fe.Params)
            {
                if (IsObf(p))
                {
                    string nn = $"_p{++_paramCnt}";
                    scope.Declare(p, nn);
                    RegisterRename(p, nn);
                }
                else scope.Declare(p, p);
            }
        }

        private void AnalyseExpr(ExprNode expr, Scope scope)
        {
            if (expr == null) return;

            switch (expr)
            {
                case FuncExpr fe:
                    var fs = new Scope(scope);
                    AnalyseFuncParams(fe, fs);
                    AnalyseBlock(fe.Body, fs);
                    break;
                case BinopExpr b:
                    AnalyseExpr(b.Left, scope);
                    AnalyseExpr(b.Right, scope);
                    break;
                case UnopExpr u:
                    AnalyseExpr(u.Operand, scope);
                    break;
                case IndexExpr ix:
                    AnalyseExpr(ix.Table, scope);
                    AnalyseExpr(ix.Key, scope);
                    break;
                case FieldExpr fx:
                    AnalyseExpr(fx.Table, scope);
                    break;
                case CallExpr ce:
                    AnalyseExpr(ce.Func, scope);
                    foreach (var a in ce.Args) AnalyseExpr(a, scope);
                    break;
                case MethodCallExpr mc:
                    AnalyseExpr(mc.Obj, scope);
                    foreach (var a in mc.Args) AnalyseExpr(a, scope);
                    break;
                case TableExpr te:
                    foreach (var f in te.Fields)
                    {
                        if (f.Key != null) AnalyseExpr(f.Key, scope);
                        AnalyseExpr(f.Value, scope);
                    }
                    break;
            }
        }

        private string GenerateName(string orig, ExprNode rhs)
        {
            if (KnownConstants != null && KnownConstants.TryGetValue(orig, out long kv))
                return SafeName($"_k{kv}");

            if (rhs is StringExpr se)
            {
                string inner = UnquoteString(se.Raw);
                if (inner.Length > 0 && inner.Length <= 24 && IsIdentLike(inner))
                    return SafeName("_s_" + inner);
                return NewVar();
            }

            if (rhs is NumberExpr ne)
                return SafeName("_n_" + ne.Raw.Replace("-", "neg").Replace(".", "p"));

            if (rhs is FuncExpr) return NewFn();
            if (rhs is TrueExpr)  return SafeName("_true");
            if (rhs is FalseExpr) return SafeName("_false");
            if (rhs is NilExpr)   return SafeName("_nil");

            return NewVar();
        }

        private string NewVar() => $"_var{++_varCnt}";
        private string NewFn()  => $"_fn{++_fnCnt}";

        private readonly HashSet<string> _usedNames = new HashSet<string>();

        private string SafeName(string candidate)
        {
            string s = Regex.Replace(candidate, @"[^A-Za-z0-9_]", "_");
            if (!_usedNames.Add(s))
            {
                int n = 2;
                while (!_usedNames.Add(s + "_" + n)) n++;
                return s + "_" + n;
            }
            return s;
        }

        private static string UnquoteString(string raw)
        {
            if (raw.Length < 2) return "";
            char q = raw[0];
            if (q != '"' && q != '\'') return "";
            return raw.Substring(1, raw.Length - 2)
                .Replace("\\n",  "")
                .Replace("\\r",  "")
                .Replace("\\t",  "")
                .Replace("\\\"", "\"")
                .Replace("\\'",  "'")
                .Replace("\\\\", "\\");
        }

        private static bool IsIdentLike(string s) =>
            Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_:./\- ]*$");

        private void RegisterRename(string orig, string newName)
        {
            if (orig != newName)
                Renames[orig] = newName;
        }

        private static bool IsObf(string name) => ObfPattern.IsMatch(name);
    }

    internal class Scope
    {
        private readonly Scope _parent;
        private readonly Dictionary<string, string> _locals = new Dictionary<string, string>();

        public Scope(Scope parent)
        {
            _parent = parent;
        }

        public void Declare(string orig, string newName)
        {
            _locals[orig] = newName;
        }

        public string Resolve(string orig)
        {
            if (_locals.TryGetValue(orig, out string v)) return v;
            return _parent?.Resolve(orig) ?? orig;
        }
    }
}
