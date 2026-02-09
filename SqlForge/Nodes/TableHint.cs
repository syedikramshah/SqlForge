using System.Collections.Generic;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class TableHint : AbstractSqlNode
    {
        public string HintName { get; set; }
        public List<ISqlNode> Parameters { get; set; } = new List<ISqlNode>();
    }
}
