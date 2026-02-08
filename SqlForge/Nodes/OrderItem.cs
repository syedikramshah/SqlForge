using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{


    /// <summary>
    /// Represents a single item in an ORDER BY clause (expression and direction).
    /// </summary>
    public class OrderItem : AbstractSqlNode
    {
        public ISqlNode Expression { get; set; }
        public bool IsAscending { get; set; } = true; // true for ASC, false for DESC
    }
}
