using System.Collections.Generic;

namespace SqlForge.Nodes
{
    public class WithClause : AbstractSqlNode
    {
        public List<CommonTableExpression> CommonTableExpressions { get; set; } = new List<CommonTableExpression>();
    }
}
