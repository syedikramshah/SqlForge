using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{

    /// <summary>
    /// Represents a binary operation (e.g., A = B, X AND Y, P + Q).
    /// </summary>
    public class BinaryExpression : AbstractSqlNode
    {
        public ISqlNode Left { get; set; }
        public string Operator { get; set; } // e.g., "=", ">", "<", "AND", "OR", "+", "-"
        public ISqlNode Right { get; set; }
    }
}
