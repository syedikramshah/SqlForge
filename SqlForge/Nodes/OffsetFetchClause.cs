using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class OffsetFetchClause : AbstractSqlNode
    {
        public ISqlNode OffsetExpression { get; set; }
        public ISqlNode FetchExpression { get; set; }
        public bool IsNext { get; set; } = true;
    }
}
