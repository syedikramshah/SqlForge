using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{

    /// <summary>
    /// Represents a FROM clause, containing tables and join expressions.
    /// </summary>
    public class FromClause : AbstractSqlNode
    {
        public List<ISqlNode> TableExpressions { get; set; } = new List<ISqlNode>();
    }
}
