using System.Collections.Generic;

namespace SqlForge.Nodes
{
    public class CommonTableExpression : AbstractSqlNode
    {
        public string Name { get; set; }
        public List<string> ColumnNames { get; set; } = new List<string>();
        public SqlStatement Query { get; set; }
    }
}
