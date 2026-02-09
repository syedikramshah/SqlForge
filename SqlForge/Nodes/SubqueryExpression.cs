using SqlForge.Enums;

namespace SqlForge.Nodes
{
    /// <summary>
    /// Represents a subquery (nested SELECT statement). Crucial for handling nested SQL.
    /// </summary>
    public class SubqueryExpression : AbstractSqlNode
    {
        public SqlStatement SubqueryStatement { get; set; }
        public string Alias { get; set; }
        public bool AliasQuoted { get; set; }
        public QuoteStyle AliasQuoteStyle { get; set; } = QuoteStyle.None;
    }
}
