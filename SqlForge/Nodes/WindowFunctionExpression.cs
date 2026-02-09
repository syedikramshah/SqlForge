using System.Collections.Generic;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class WindowFunctionExpression : AbstractSqlNode
    {
        public string FunctionName { get; set; }
        public List<ISqlNode> Arguments { get; set; } = new List<ISqlNode>();
        public List<ISqlNode> PartitionByExpressions { get; set; } = new List<ISqlNode>();
        public List<OrderItem> OrderByItems { get; set; } = new List<OrderItem>();
        public WindowFrame Frame { get; set; }
    }
}
