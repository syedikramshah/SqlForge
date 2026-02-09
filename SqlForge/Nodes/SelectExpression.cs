using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    /// <summary>
    /// Represents an individual item in the SELECT list, which can be an expression and have an alias.
    /// </summary>
    public class SelectExpression : AbstractSqlNode
    {
        public ISqlNode Expression { get; set; }
        public string Alias { get; set; }
        public bool AliasQuoted { get; set; }
        public QuoteStyle AliasQuoteStyle { get; set; } = QuoteStyle.None;
        public bool HasExplicitAs { get; set; }
    }
}
