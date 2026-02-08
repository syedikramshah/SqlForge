using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    /// <summary>
    /// Represents a HAVING clause, containing a single condition expression.
    /// </summary>
    public class HavingClause : AbstractSqlNode
    {
        public ISqlNode Condition { get; set; }
    }
}
