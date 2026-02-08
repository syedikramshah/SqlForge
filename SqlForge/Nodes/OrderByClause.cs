using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge;

namespace SqlForge.Nodes
{

    /// <summary>
    /// Represents an ORDER BY clause, containing a list of order by items.
    /// </summary>
    public class OrderByClause : AbstractSqlNode
    {
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

}
