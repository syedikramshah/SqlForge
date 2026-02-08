using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Enums
{
    /// <summary>
    /// Represents the type of a SQL statement.
    /// </summary>
    public enum StatementType
    {
        Unknown,
        Select,
        Insert,
        Update,
        Delete,
        Create,
        Drop,
        Alter
    }
}
