using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge;
using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    // ============================================================================
    // PLANNED ENHANCEMENTS
    // ============================================================================
    //
    // 1. Set Operations (UNION, INTERSECT, EXCEPT)
    //    -----------------------------------------
    //    Currently handled via SetOperatorExpression as a separate node type.
    //    Consider whether to integrate directly into SelectStatement for
    //    simpler compound query representation.
    //
    // 2. TOP/LIMIT Clause Support
    //    ------------------------
    //    Add TopCount property for SQL Server style: SELECT TOP 10 ...
    //    Add LimitClause for MySQL/PostgreSQL style: ... LIMIT 10 OFFSET 5
    //
    //    Implementation approach:
    //    - Add TopCount (int?) and TopPercent (bool) for TOP clause
    //    - Add LimitCount (int?) and OffsetCount (int?) for LIMIT/OFFSET
    //    - Parser should detect TOP after SELECT or LIMIT at end of statement
    //    - Dialect-aware reconstruction (TOP vs LIMIT based on SqlDialect)
    //
    // 3. WITH Clause (CTEs - Common Table Expressions)
    //    ---------------------------------------------
    //    Add WithClause property to hold CTE definitions.
    //    Example: WITH cte AS (SELECT ...) SELECT * FROM cte
    //
    // ============================================================================

    /// <summary>
    /// Represents a SELECT statement, including its clauses.
    /// </summary>
    public class SelectStatement : AbstractSqlNode
    {
        public List<SelectExpression> SelectItems { get; set; } = new List<SelectExpression>();
        public FromClause FromClause { get; set; }
        public WhereClause WhereClause { get; set; }
        public GroupByClause GroupByClause { get; set; }
        public HavingClause HavingClause { get; set; }
        public OrderByClause OrderByClause { get; set; }
        public TopClause TopClause { get; set; }
        public OffsetFetchClause OffsetFetchClause { get; set; }
        public bool IsDistinct { get; set; }
    }
}
