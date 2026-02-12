using System.Collections.Generic;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class CreateTableStatement : AbstractSqlNode
    {
        public bool IsTemporary { get; set; }
        public bool IfNotExists { get; set; }
        public TableExpression Target { get; set; }
        public List<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();
        public List<ITableConstraint> Constraints { get; set; } = new List<ITableConstraint>();
        public List<IndexDefinition> Indexes { get; set; } = new List<IndexDefinition>();
        public List<TableOption> TableOptions { get; set; } = new List<TableOption>();
    }
}
