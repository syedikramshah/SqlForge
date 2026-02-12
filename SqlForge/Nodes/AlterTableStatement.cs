using System.Collections.Generic;

namespace SqlForge.Nodes
{
    public class AlterTableStatement : AbstractSqlNode
    {
        public TableExpression Target { get; set; }
        public List<AlterTableAction> Actions { get; set; } = new List<AlterTableAction>();
    }
}
