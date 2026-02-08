using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Nodes
{
    // ============================================================================
    // PLANNED ENHANCEMENTS
    // ============================================================================
    //
    // 1. Subquery Context Properties
    //    ---------------------------
    //    Add properties to indicate the context in which the subquery is used:
    //    - IsExistsContext: true when used with EXISTS operator
    //    - IsInContext: true when used with IN operator
    //    - IsAnyContext: true when used with ANY/SOME operator
    //    - IsAllContext: true when used with ALL operator
    //    - IsScalarContext: true when used as scalar subquery in SELECT list
    //
    //    Implementation approach:
    //    - Add SubqueryContext enum (Exists, In, Any, All, Scalar, DerivedTable)
    //    - Set context during parsing based on surrounding syntax
    //    - Useful for query optimization hints and semantic analysis
    //
    // ============================================================================

    /// <summary>
    /// Represents a subquery (nested SELECT statement). Crucial for handling nested SQL.
    /// </summary>
    public class SubqueryExpression : AbstractSqlNode
    {
        public SqlStatement SubqueryStatement { get; set; }
        public string Alias { get; set; }
        public bool AliasQuoted { get; set; }
    }
}
