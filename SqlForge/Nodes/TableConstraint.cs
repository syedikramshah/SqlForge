using System.Collections.Generic;
using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class TableConstraint : AbstractSqlNode, ITableConstraint
    {
        public ConstraintType Type { get; set; }
        public string Name { get; set; }
        public bool NameQuoted { get; set; }
        public QuoteStyle NameQuoteStyle { get; set; } = QuoteStyle.None;
        public List<ColumnExpression> Columns { get; set; } = new List<ColumnExpression>();
    }
}
