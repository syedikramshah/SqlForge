using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{

    /// <summary>
    /// Represents a complete SQL statement (e.g., SELECT, INSERT, UPDATE).
    /// </summary>
    public class SqlStatement : AbstractSqlNode
    {
        public StatementType Type { get; set; }
        public ISqlNode Body { get; set; }
    }
}
