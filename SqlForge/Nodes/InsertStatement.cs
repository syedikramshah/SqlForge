using System.Collections.Generic;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class InsertStatement : AbstractSqlNode
    {
        public bool IsReplace { get; set; }
        public bool IsIgnore { get; set; }
        public TableExpression Target { get; set; }
        public List<ColumnExpression> Columns { get; set; } = new List<ColumnExpression>();
        public List<List<ISqlNode>> Values { get; set; } = new List<List<ISqlNode>>();
        public List<AssignmentExpression> SetAssignments { get; set; } = new List<AssignmentExpression>();
        public SqlStatement SelectStatement { get; set; }
        public List<AssignmentExpression> OnDuplicateKeyUpdate { get; set; } = new List<AssignmentExpression>();
    }
}
