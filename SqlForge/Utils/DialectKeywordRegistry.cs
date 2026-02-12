using System;
using System.Collections.Generic;
using SqlForge.Enums;

namespace SqlForge.Utils
{
    /// <summary>
    /// Central registry for SQL keywords with dialect-aware overrides.
    /// </summary>
    public static class DialectKeywordRegistry
    {
        private static readonly HashSet<string> CommonKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "AND", "OR", "GROUP", "BY", "HAVING", "ORDER", "AS",
            "INSERT", "UPDATE", "DELETE", "CREATE", "TABLE", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "ON",
            "DISTINCT", "DISTINCTROW", "TOP", "UNION", "ALL", "EXCEPT", "INTERSECT", "IN", "NOT", "NULL", "IS",
            "COUNT", "SUM", "AVG", "MIN", "MAX", "SUBSTRING", "GETDATE",
            "CASE", "WHEN", "THEN", "ELSE", "END", "EXISTS", "OUTER",
            "LIKE", "ASC", "DESC",
            "WITH", "OVER", "PARTITION", "OFFSET", "FETCH", "ROWS", "ROW", "ONLY", "LIMIT",
            "APPLY", "CROSS", "PERCENT", "TIES", "RANGE", "GROUPS", "NEXT", "FIRST",
            "BETWEEN", "FOLLOWING", "PRECEDING", "UNBOUNDED", "CURRENT",
            "HIGH_PRIORITY", "STRAIGHT_JOIN", "SQL_SMALL_RESULT", "SQL_BIG_RESULT", "SQL_BUFFER_RESULT",
            "SQL_CACHE", "SQL_NO_CACHE", "SQL_CALC_FOUND_ROWS",
            "INTO", "OUTFILE", "DUMPFILE", "FOR", "LOCK", "SHARE", "MODE", "ROLLUP",
            "IGNORE", "LOW_PRIORITY", "QUICK", "REPLACE", "VALUES", "SET",
            "DUPLICATE", "KEY", "IF", "TEMPORARY",
            "UNIQUE", "PRIMARY", "INDEX", "ADD", "DROP", "ALTER", "MODIFY", "CHANGE", "COLUMN", "RENAME",
            "TRUE", "FALSE", "UNKNOWN", "AUTO_INCREMENT",
            "UNSIGNED", "ZEROFILL", "BINARY",
            "ENGINE", "CHARSET", "CHARACTER", "COLLATE", "COMMENT", "DEFAULT",
            "CONSTRAINT", "FOREIGN", "REFERENCES", "CHECK", "CASCADE", "RESTRICT", "NO", "ACTION", "INCLUDE",
            "FULLTEXT", "SPATIAL", "USING", "BTREE", "HASH", "TO",
            "IDENTITY"
        };

        private static readonly IReadOnlyDictionary<SqlDialect, HashSet<string>> DialectSpecificKeywords =
            new Dictionary<SqlDialect, HashSet<string>>
            {
                [SqlDialect.MsSqlServer] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "MERGE",
                    "OUTPUT"
                },
                [SqlDialect.MySql] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "REGEXP",
                    "RLIKE",
                    "DIV",
                    "MOD",
                    "XOR"
                }
            };

        private static readonly IReadOnlyDictionary<SqlDialect, HashSet<string>> DialectExclusions =
            new Dictionary<SqlDialect, HashSet<string>>
            {
                [SqlDialect.SqlAnywhere] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "REGEXP",
                    "RLIKE",
                    "DIV",
                    "MOD",
                    "XOR"
                },
                [SqlDialect.MsSqlServer] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "REGEXP",
                    "RLIKE",
                    "DIV",
                    "MOD",
                    "XOR"
                }
            };

        public static bool IsKeyword(string value, SqlDialect dialect = SqlDialect.Generic)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (dialect == SqlDialect.Generic)
            {
                if (CommonKeywords.Contains(value))
                {
                    return true;
                }

                foreach (var dialectSet in DialectSpecificKeywords.Values)
                {
                    if (dialectSet.Contains(value))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (DialectExclusions.TryGetValue(dialect, out var exclusions) && exclusions.Contains(value))
            {
                return false;
            }

            if (CommonKeywords.Contains(value))
            {
                return true;
            }

            return DialectSpecificKeywords.TryGetValue(dialect, out var specific) && specific.Contains(value);
        }
    }
}
