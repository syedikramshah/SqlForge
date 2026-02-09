using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Enums
{


    /// <summary>
    /// Represents different types of SQL JOIN operations.
    /// </summary>
    public enum JoinType
    {
        Unknown,
        Inner,
        Left,
        Right,
        Full,
        Cross,
        Natural,
        CrossApply,
        OuterApply
    }
}
