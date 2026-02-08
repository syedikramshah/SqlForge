using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Interfaces
{

    /// <summary>
    /// Defines the contract for formatting an AST into a well-structured, human-readable output.
    /// </summary>
    public interface ISqlFormatter
    {
        string Format(ISqlNode node);
    }
}
