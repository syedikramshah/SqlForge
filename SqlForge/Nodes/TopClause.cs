using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class TopClause : AbstractSqlNode
    {
        public ISqlNode Expression { get; set; }
        public bool IsPercent { get; set; }
        public bool WithTies { get; set; }
    }
}
