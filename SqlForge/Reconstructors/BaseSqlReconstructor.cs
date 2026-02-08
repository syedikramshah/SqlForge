using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Enums;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Reconstructors
{
    /// <summary>
    /// Base class for SQL reconstructors.
    /// </summary>
    public abstract class BaseSqlReconstructor : ISqlReconstructor
    {
        public abstract string Reconstruct(SqlStatement statement, SqlDialect dialect = SqlDialect.Generic);
    }
}
