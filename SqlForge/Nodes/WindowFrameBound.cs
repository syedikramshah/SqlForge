using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class WindowFrameBound : AbstractSqlNode
    {
        public WindowFrameBoundType Type { get; set; }
        public ISqlNode OffsetExpression { get; set; }
    }
}
