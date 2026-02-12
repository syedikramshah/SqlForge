using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class AlterTableAction : AbstractSqlNode
    {
        public AlterTableActionType ActionType { get; set; }
        public ColumnDefinition Column { get; set; }
        public ITableConstraint Constraint { get; set; }
        public string ConstraintName { get; set; }
        public bool ConstraintNameQuoted { get; set; }
        public QuoteStyle ConstraintNameQuoteStyle { get; set; } = QuoteStyle.None;
        public string ColumnName { get; set; }
        public bool ColumnNameQuoted { get; set; }
        public QuoteStyle ColumnNameQuoteStyle { get; set; } = QuoteStyle.None;
        public string NewTableName { get; set; }
        public bool NewTableNameQuoted { get; set; }
        public QuoteStyle NewTableNameQuoteStyle { get; set; } = QuoteStyle.None;
        public string NewColumnName { get; set; }
        public bool NewColumnNameQuoted { get; set; }
        public QuoteStyle NewColumnNameQuoteStyle { get; set; } = QuoteStyle.None;
        public IndexDefinition Index { get; set; }
        public string IndexName { get; set; }
        public bool DropPrimaryKey { get; set; }
    }
}
