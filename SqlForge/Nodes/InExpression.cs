using System.Collections.Generic;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Nodes
{
    public class InExpression : AbstractSqlNode
    {
        public ISqlNode Expression { get; set; } // e.g. "UserID"
        public SqlStatement Subquery { get; set; }     // e.g. "(SELECT ...)"
        public List<ISqlNode> Values { get; } = new List<ISqlNode>(); // IN (value, ...)
        public bool IsNegated { get; set; }            // "NOT IN" vs "IN"
    }
}
