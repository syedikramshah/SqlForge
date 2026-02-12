using System.Collections.Generic;

namespace SqlForge.Nodes
{
    public class UpdateStatement : AbstractSqlNode
    {
        public bool IsLowPriority { get; set; }
        public bool IsIgnore { get; set; }
        public TableExpression Target { get; set; }
        public List<AssignmentExpression> SetClauses { get; set; } = new List<AssignmentExpression>();
        public WhereClause WhereClause { get; set; }
        public OrderByClause OrderByClause { get; set; }
        public LimitClause LimitClause { get; set; }
    }
}
