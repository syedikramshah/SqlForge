using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{

    /// <summary>
    /// Represents a GROUP BY clause, containing a list of expressions.
    /// </summary>
    public class GroupByClause : AbstractSqlNode
    {
        public List<ISqlNode> GroupingExpressions { get; set; } = new List<ISqlNode>();
    }
}
