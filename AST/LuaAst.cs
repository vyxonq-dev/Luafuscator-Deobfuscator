using System.Collections.Generic;

namespace _1._0._8._0_D.ast
{
    public abstract class LuaNode
    {
        public int Line;
    }

    public class BlockNode : LuaNode
    {
        public List<LuaNode> Stmts = new List<LuaNode>();
    }

    public class AssignNode : LuaNode
    {
        public List<ExprNode> Targets;
        public List<ExprNode> Values;
    }

    public class LocalNode : LuaNode
    {
        public List<string>   Names;
        public List<string>   Attrs;
        public List<ExprNode> Values;
    }

    public class DoNode : LuaNode
    {
        public BlockNode Body;
    }

    public class WhileNode : LuaNode
    {
        public ExprNode  Cond;
        public BlockNode Body;
    }

    public class RepeatNode : LuaNode
    {
        public BlockNode Body;
        public ExprNode  Cond;
    }

    public class IfNode : LuaNode
    {
        public List<(ExprNode Cond, BlockNode Body)> Clauses = new List<(ExprNode, BlockNode)>();
        public BlockNode Else;
    }

    public class ForNumNode : LuaNode
    {
        public string    Var;
        public ExprNode  Start;
        public ExprNode  Limit;
        public ExprNode  Step;
        public BlockNode Body;
    }

    public class ForInNode : LuaNode
    {
        public List<string>   Vars;
        public List<ExprNode> Iters;
        public BlockNode      Body;
    }

    public class FuncStmtNode : LuaNode
    {
        public List<string> Names;
        public string       MethodName;
        public FuncExpr     Func;
    }

    public class LocalFuncNode : LuaNode
    {
        public string   Name;
        public FuncExpr Func;
    }

    public class ReturnNode : LuaNode
    {
        public List<ExprNode> Values;
    }

    public class BreakNode : LuaNode { }

    public class GotoNode : LuaNode
    {
        public string Label;
    }

    public class LabelNode : LuaNode
    {
        public string Name;
    }

    public class CallStmtNode : LuaNode
    {
        public ExprNode Call;
    }

    public abstract class ExprNode : LuaNode { }

    public class NilExpr     : ExprNode { }
    public class TrueExpr    : ExprNode { }
    public class FalseExpr   : ExprNode { }
    public class VarArgExpr  : ExprNode { }

    public class NumberExpr : ExprNode
    {
        public string Raw;
    }

    public class StringExpr : ExprNode
    {
        public string Raw;
    }

    public class NameExpr : ExprNode
    {
        public string Name;
    }

    public class BinopExpr : ExprNode
    {
        public string  Op;
        public ExprNode Left;
        public ExprNode Right;
    }

    public class UnopExpr : ExprNode
    {
        public string  Op;
        public ExprNode Operand;
    }

    public class IndexExpr : ExprNode
    {
        public ExprNode Table;
        public ExprNode Key;
    }

    public class FieldExpr : ExprNode
    {
        public ExprNode Table;
        public string   Field;
    }

    public class MethodCallExpr : ExprNode
    {
        public ExprNode      Obj;
        public string        Method;
        public List<ExprNode> Args;
    }

    public class CallExpr : ExprNode
    {
        public ExprNode       Func;
        public List<ExprNode> Args;
    }

    public class TableExpr : ExprNode
    {
        public List<TableField> Fields;
    }

    public class FuncExpr : ExprNode
    {
        public List<string> Params;
        public bool         HasVarArg;
        public BlockNode    Body;
    }

    public class TableField : LuaNode
    {
        public ExprNode Key;
        public ExprNode Value;
    }
}
