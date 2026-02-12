using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class BetweenExpression : AbstractSqlNode
    {
        public ISqlNode Expression { get; set; }
        public ISqlNode Lower { get; set; }
        public ISqlNode Upper { get; set; }
        public bool IsNegated { get; set; }
    }
}
