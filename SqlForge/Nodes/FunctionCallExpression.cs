using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    /// <summary>
    /// Represents a function call (e.g., COUNT(*), SUBSTRING(col, 1, 5)).
    /// </summary>
    public class FunctionCallExpression : AbstractSqlNode
    {
        public string FunctionName { get; set; }
        public List<ISqlNode> Arguments { get; set; } = new List<ISqlNode>();
        public bool IsAllColumns { get; set; }
    }
}
