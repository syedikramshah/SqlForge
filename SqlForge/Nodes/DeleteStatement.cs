namespace SqlForge.Nodes
{
    public class DeleteStatement : AbstractSqlNode
    {
        public bool IsLowPriority { get; set; }
        public bool IsQuick { get; set; }
        public bool IsIgnore { get; set; }
        public TableExpression Target { get; set; }
        public WhereClause WhereClause { get; set; }
        public OrderByClause OrderByClause { get; set; }
        public LimitClause LimitClause { get; set; }
    }
}
