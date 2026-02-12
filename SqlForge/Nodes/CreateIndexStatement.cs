using System.Collections.Generic;

namespace SqlForge.Nodes
{
    public class CreateIndexStatement : AbstractSqlNode
    {
        public IndexDefinition Index { get; set; } = new IndexDefinition();
        public TableExpression Target { get; set; }
        public List<ColumnExpression> IncludeColumns { get; set; } = new List<ColumnExpression>();
    }
}
