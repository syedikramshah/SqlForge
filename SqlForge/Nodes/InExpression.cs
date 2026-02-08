using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Nodes
{
    public class InExpression : AbstractSqlNode
    {
        public ISqlNode Expression { get; set; } // e.g. "UserID"
        public SqlStatement Subquery { get; set; }     // e.g. "(SELECT ...)"
        public bool IsNegated { get; set; }            // "NOT IN" vs "IN"
    }
}
