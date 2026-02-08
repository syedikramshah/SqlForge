using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge;

namespace SqlForge.Nodes
{

    /// <summary>
    /// Represents a single table or a derived table (subquery) in the FROM clause.
    /// </summary>
    public class TableExpression : AbstractSqlNode
    {
        public string TableName { get; set; }
        public string SchemaName { get; set; }
        public string Alias { get; set; }
        public SubqueryExpression Subquery { get; set; }
        public bool SchemaNameQuoted { get; set; }
        public bool TableNameQuoted { get; set; }
        public bool AliasQuoted { get; set; }
        public bool HasExplicitAs { get; set; }
    }
}