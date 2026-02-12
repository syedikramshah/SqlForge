using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class ColumnDefinition : AbstractSqlNode
    {
        public string Name { get; set; }
        public bool NameQuoted { get; set; }
        public QuoteStyle NameQuoteStyle { get; set; } = QuoteStyle.None;
        public string DataType { get; set; }
        public bool? IsNullable { get; set; }
        public ISqlNode DefaultExpression { get; set; }
        public bool AutoIncrement { get; set; }
        public bool IsIdentity { get; set; }
        public ISqlNode IdentitySeed { get; set; }
        public ISqlNode IdentityIncrement { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsUnique { get; set; }
        public string CharacterSet { get; set; }
        public string Collation { get; set; }
        public string Comment { get; set; }
    }
}
