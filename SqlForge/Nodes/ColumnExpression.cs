using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Enums;

namespace SqlForge.Nodes
{
    /// <summary>
    /// Represents a column reference in a query.
    /// </summary>
    public class ColumnExpression : AbstractSqlNode
    {
        public string ColumnName { get; set; }
        public string TableAlias { get; set; }
        public string Alias { get; set; }
        public string SchemaName { get; set; }

        public bool ColumnNameQuoted { get; set; }   
        public bool TableAliasQuoted { get; set; }    
        public bool SchemaNameQuoted { get; set; }

        public QuoteStyle ColumnQuoteStyle { get; set; } = QuoteStyle.None;
        public QuoteStyle TableAliasQuoteStyle { get; set; } = QuoteStyle.None;
        public QuoteStyle SchemaQuoteStyle { get; set; } = QuoteStyle.None;
    }
}
