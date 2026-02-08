using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{


    /// <summary>
    /// Represents a unary operation (e.g., NOT X, -Y).
    /// </summary>
    public class UnaryExpression : AbstractSqlNode
    {
        public string Operator { get; set; } // e.g., "NOT", "-"
        public ISqlNode Expression { get; set; }
    }
}
