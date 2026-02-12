using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class CheckConstraint : AbstractSqlNode, ITableConstraint
    {
        public string Name { get; set; }
        public bool NameQuoted { get; set; }
        public Enums.QuoteStyle NameQuoteStyle { get; set; } = Enums.QuoteStyle.None;
        public ISqlNode Condition { get; set; }
    }
}
