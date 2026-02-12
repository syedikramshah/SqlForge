using System.Collections.Generic;
using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class ForeignKeyConstraint : AbstractSqlNode, ITableConstraint
    {
        public string Name { get; set; }
        public bool NameQuoted { get; set; }
        public QuoteStyle NameQuoteStyle { get; set; } = QuoteStyle.None;
        public List<ColumnExpression> Columns { get; set; } = new List<ColumnExpression>();
        public TableExpression ReferencedTable { get; set; }
        public List<ColumnExpression> ReferencedColumns { get; set; } = new List<ColumnExpression>();
        public ReferentialAction? OnDelete { get; set; }
        public ReferentialAction? OnUpdate { get; set; }
    }
}
