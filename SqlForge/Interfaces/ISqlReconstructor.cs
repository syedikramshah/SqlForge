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
    /// Defines the contract for reconstructing a SQL string from an AST.
    /// </summary>
    public interface ISqlReconstructor
    {
        string Reconstruct(SqlStatement statement, SqlDialect dialect = SqlDialect.Generic);
    }
}
