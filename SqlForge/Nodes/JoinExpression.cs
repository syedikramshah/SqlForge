using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Enums;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    // ============================================================================
    // PLANNED ENHANCEMENTS
    // ============================================================================
    //
    // 1. USING Clause Support
    //    --------------------
    //    Add support for the USING clause syntax as an alternative to ON.
    //    Example: SELECT * FROM TableA JOIN TableB USING (ColumnName)
    //
    //    Implementation approach:
    //    - Add UsingColumns property (List<string>) to hold column names
    //    - Parser should detect USING keyword after JOIN and parse column list
    //    - Reconstructor should emit USING (col1, col2) when UsingColumns is set
    //    - OnCondition and UsingColumns are mutually exclusive
    //
    // ============================================================================

    /// <summary>
    /// Represents a JOIN operation between two tables/expressions.
    /// </summary>
    public class JoinExpression : AbstractSqlNode
    {
        public JoinType Type { get; set; }
        public ISqlNode Left { get; set; }
        public ISqlNode Right { get; set; }
        public ISqlNode OnCondition { get; set; }
    }
}
