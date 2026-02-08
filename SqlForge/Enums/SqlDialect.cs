using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Enums
{
    /// <summary>
    /// Represents different SQL dialects that the parser can support.
    /// </summary>
    public enum SqlDialect
    {
        Generic,
        SqlAnywhere,
        MsSqlServer,
        MySql,
        PostgreSql
        // Add more as needed
    }

}
