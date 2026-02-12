namespace SqlForge.Nodes
{
    public class DropIndexStatement : AbstractSqlNode
    {
        public string IndexName { get; set; }
        public bool IndexNameQuoted { get; set; }
        public Enums.QuoteStyle IndexNameQuoteStyle { get; set; } = Enums.QuoteStyle.None;
        public bool IfExists { get; set; }
        public TableExpression Target { get; set; }
    }
}
