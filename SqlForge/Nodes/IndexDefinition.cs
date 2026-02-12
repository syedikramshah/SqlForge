using System.Collections.Generic;
using SqlForge.Enums;

namespace SqlForge.Nodes
{
    public class IndexDefinition : AbstractSqlNode
    {
        public IndexType Type { get; set; }
        public string Name { get; set; }
        public bool NameQuoted { get; set; }
        public QuoteStyle NameQuoteStyle { get; set; } = QuoteStyle.None;
        public List<IndexColumn> Columns { get; set; } = new List<IndexColumn>();
        public string UsingType { get; set; }
    }
}
