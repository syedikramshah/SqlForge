using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{


    /// <summary>
    /// Represents a WHERE clause, containing a single condition expression.
    /// </summary>
    public class WhereClause : AbstractSqlNode
    {
        public ISqlNode Condition { get; set; }
    }
}
