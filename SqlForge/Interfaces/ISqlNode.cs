using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Interfaces
{
    // ============================================================================
    // PLANNED ENHANCEMENTS
    // ============================================================================
    //
    // 1. SourceSpan Property for Error Reporting
    //    -------------------------------------
    //    Add StartIndex and EndIndex properties to track the original position
    //    of each node in the source SQL string. This enables:
    //    - Precise error messages pointing to exact location in source
    //    - Syntax highlighting in IDE integrations
    //    - Source mapping for debugging and tooling
    //
    //    Implementation approach:
    //    - Add SourceSpan struct with StartIndex, EndIndex, and Length properties
    //    - Capture token positions during parsing and propagate to AST nodes
    //    - Consider memory impact for large ASTs
    //
    // ============================================================================

    /// <summary>
    /// Base interface for all nodes in the Abstract Syntax Tree (AST).
    /// </summary>
    public interface ISqlNode
    {
    }
}
