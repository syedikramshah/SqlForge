using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge;
using SqlForge.Enums;
using SqlForge.Nodes;

namespace SqlForge.Interfaces
{

    /// <summary>
    /// Defines the contract for parsing a SQL string into an AST.
    /// </summary>
    public interface ISqlParser
    {
        SqlStatement Parse(string sql, SqlDialect dialect = SqlDialect.Generic);
    }
}
