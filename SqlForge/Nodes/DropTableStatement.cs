using System.Collections.Generic;

namespace SqlForge.Nodes
{
    public class DropTableStatement : AbstractSqlNode
    {
        public bool IfExists { get; set; }
        public List<TableExpression> Targets { get; set; } = new List<TableExpression>();
    }
}
