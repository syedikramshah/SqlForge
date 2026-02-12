using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class LimitClause : AbstractSqlNode
    {
        public ISqlNode OffsetExpression { get; set; }
        public ISqlNode CountExpression { get; set; }
    }
}
