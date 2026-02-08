using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Enums;

namespace SqlForge.Nodes
{

    /// <summary>
    /// Represents a literal value (e.g., 'text', 123, TRUE, NULL).
    /// </summary>
    public class LiteralExpression : AbstractSqlNode
    {
        public string Value { get; set; }
        public LiteralType Type { get; set; }
    }
}
