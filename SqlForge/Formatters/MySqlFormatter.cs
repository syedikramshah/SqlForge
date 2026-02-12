using System.Text;
using SqlForge.Enums;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Formatters
{
    /// <summary>
    /// Implements ISqlFormatter for MySQL dialect, producing a readable AST representation.
    /// </summary>
    public class MySqlFormatter : BaseSqlFormatter
    {
        public override string Format(ISqlNode node)
        {
            var sb = new StringBuilder();
            FormatNode(node, sb, 0);
            return sb.ToString();
        }

        private void FormatNode(ISqlNode node, StringBuilder sb, int indentLevel)
        {
            if (node == null)
            {
                return;
            }

            var indent = new string(' ', indentLevel * 4);
            sb.AppendLine($"{indent}{node.GetType().Name}:");

            switch (node)
            {
                case SqlStatement stmt:
                    sb.AppendLine($"{indent}    Type: {stmt.Type}");
                    if (stmt.WithClause != null)
                    {
                        FormatNode(stmt.WithClause, sb, indentLevel + 1);
                    }
                    FormatNode(stmt.Body, sb, indentLevel + 1);
                    break;

                case WithClause withClause:
                    sb.AppendLine($"{indent}    CommonTableExpressions:");
                    foreach (var cte in withClause.CommonTableExpressions)
                    {
                        FormatNode(cte, sb, indentLevel + 2);
                    }
                    break;

                case CommonTableExpression cte:
                    sb.AppendLine($"{indent}    Name: {cte.Name}");
                    if (cte.ColumnNames.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Columns: {string.Join(", ", cte.ColumnNames)}");
                    }
                    sb.AppendLine($"{indent}    Query:");
                    FormatNode(cte.Query, sb, indentLevel + 2);
                    break;

                case SelectStatement select:
                    if (select.IsDistinct) sb.AppendLine($"{indent}    IsDistinct: True");
                    if (select.IsDistinctRow) sb.AppendLine($"{indent}    IsDistinctRow: True");
                    if (select.SelectModifiers != null && select.SelectModifiers.Count > 0)
                        sb.AppendLine($"{indent}    SelectModifiers: {string.Join(", ", select.SelectModifiers)}");
                    sb.AppendLine($"{indent}    SelectItems:");
                    foreach (var item in select.SelectItems) FormatNode(item, sb, indentLevel + 2);
                    if (select.IntoClause != null) FormatNode(select.IntoClause, sb, indentLevel + 1);
                    if (select.FromClause != null) FormatNode(select.FromClause, sb, indentLevel + 1);
                    if (select.WhereClause != null) FormatNode(select.WhereClause, sb, indentLevel + 1);
                    if (select.GroupByClause != null) FormatNode(select.GroupByClause, sb, indentLevel + 1);
                    if (select.HavingClause != null) FormatNode(select.HavingClause, sb, indentLevel + 1);
                    if (select.OrderByClause != null) FormatNode(select.OrderByClause, sb, indentLevel + 1);
                    if (select.LimitClause != null) FormatNode(select.LimitClause, sb, indentLevel + 1);
                    if (select.ForUpdate) sb.AppendLine($"{indent}    ForUpdate: True");
                    if (select.LockInShareMode) sb.AppendLine($"{indent}    LockInShareMode: True");
                    break;

                case SelectIntoClause intoClause:
                    sb.AppendLine($"{indent}    IntoType: {intoClause.Type}");
                    sb.AppendLine($"{indent}    FilePath: {intoClause.FilePath}");
                    break;

                case CreateTableStatement create:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(create.Target, sb, indentLevel + 2);
                    if (create.Columns.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Columns:");
                        foreach (var col in create.Columns) FormatNode(col, sb, indentLevel + 2);
                    }
                    if (create.Constraints.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Constraints:");
                        foreach (var constraint in create.Constraints) FormatNode(constraint, sb, indentLevel + 2);
                    }
                    if (create.Indexes.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Indexes:");
                        foreach (var idx in create.Indexes) FormatNode(idx, sb, indentLevel + 2);
                    }
                    if (create.TableOptions.Count > 0)
                    {
                        sb.AppendLine($"{indent}    TableOptions:");
                        foreach (var opt in create.TableOptions) FormatNode(opt, sb, indentLevel + 2);
                    }
                    break;

                case ColumnDefinition column:
                    sb.AppendLine($"{indent}    Name: {QuoteIdentifier(column.Name, column.NameQuoteStyle, column.NameQuoted)}");
                    sb.AppendLine($"{indent}    DataType: {column.DataType}");
                    if (column.IsNullable.HasValue)
                        sb.AppendLine($"{indent}    Nullable: {column.IsNullable}");
                    if (column.IsPrimaryKey) sb.AppendLine($"{indent}    PrimaryKey: True");
                    if (column.IsUnique) sb.AppendLine($"{indent}    Unique: True");
                    if (!string.IsNullOrEmpty(column.CharacterSet))
                        sb.AppendLine($"{indent}    CharacterSet: {column.CharacterSet}");
                    if (!string.IsNullOrEmpty(column.Collation))
                        sb.AppendLine($"{indent}    Collation: {column.Collation}");
                    if (!string.IsNullOrEmpty(column.Comment))
                        sb.AppendLine($"{indent}    Comment: {column.Comment}");
                    if (column.DefaultExpression != null)
                    {
                        sb.AppendLine($"{indent}    DefaultExpression:");
                        FormatNode(column.DefaultExpression, sb, indentLevel + 2);
                    }
                    if (column.AutoIncrement) sb.AppendLine($"{indent}    AutoIncrement: True");
                    break;

                case IndexDefinition index:
                    sb.AppendLine($"{indent}    IndexType: {index.Type}");
                    if (!string.IsNullOrEmpty(index.Name)) sb.AppendLine($"{indent}    Name: {index.Name}");
                    if (!string.IsNullOrEmpty(index.UsingType)) sb.AppendLine($"{indent}    Using: {index.UsingType}");
                    sb.AppendLine($"{indent}    Columns:");
                    foreach (var col in index.Columns) FormatNode(col, sb, indentLevel + 2);
                    break;

                case IndexColumn idxCol:
                    sb.AppendLine($"{indent}    Column:");
                    FormatNode(idxCol.Column, sb, indentLevel + 2);
                    if (idxCol.LengthExpression != null)
                    {
                        sb.AppendLine($"{indent}    Length:");
                        FormatNode(idxCol.LengthExpression, sb, indentLevel + 2);
                    }
                    if (idxCol.IsAscending.HasValue)
                        sb.AppendLine($"{indent}    Direction: {(idxCol.IsAscending.Value ? "ASC" : "DESC")}");
                    break;

                case TableOption option:
                    sb.AppendLine($"{indent}    Name: {option.Name}");
                    if (!string.IsNullOrEmpty(option.Value))
                        sb.AppendLine($"{indent}    Value: {option.Value}");
                    break;

                case AlterTableStatement alter:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(alter.Target, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Actions:");
                    foreach (var action in alter.Actions) FormatNode(action, sb, indentLevel + 2);
                    break;

                case AlterTableAction action:
                    sb.AppendLine($"{indent}    ActionType: {action.ActionType}");
                    if (action.Column != null) FormatNode(action.Column, sb, indentLevel + 1);
                    if (action.Constraint != null) FormatNode(action.Constraint, sb, indentLevel + 1);
                    if (action.Index != null) FormatNode(action.Index, sb, indentLevel + 1);
                    if (!string.IsNullOrEmpty(action.ColumnName)) sb.AppendLine($"{indent}    ColumnName: {action.ColumnName}");
                    if (!string.IsNullOrEmpty(action.NewColumnName)) sb.AppendLine($"{indent}    NewColumnName: {action.NewColumnName}");
                    if (!string.IsNullOrEmpty(action.NewTableName)) sb.AppendLine($"{indent}    NewTableName: {action.NewTableName}");
                    if (!string.IsNullOrEmpty(action.IndexName)) sb.AppendLine($"{indent}    IndexName: {action.IndexName}");
                    if (action.DropPrimaryKey) sb.AppendLine($"{indent}    DropPrimaryKey: True");
                    break;
                case ForeignKeyConstraint foreignKey:
                    if (!string.IsNullOrEmpty(foreignKey.Name)) sb.AppendLine($"{indent}    Name: {foreignKey.Name}");
                    sb.AppendLine($"{indent}    Columns:");
                    foreach (var col in foreignKey.Columns) FormatNode(col, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    ReferencedTable:");
                    FormatNode(foreignKey.ReferencedTable, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    ReferencedColumns:");
                    foreach (var col in foreignKey.ReferencedColumns) FormatNode(col, sb, indentLevel + 2);
                    if (foreignKey.OnDelete.HasValue) sb.AppendLine($"{indent}    OnDelete: {foreignKey.OnDelete}");
                    if (foreignKey.OnUpdate.HasValue) sb.AppendLine($"{indent}    OnUpdate: {foreignKey.OnUpdate}");
                    break;
                case CheckConstraint check:
                    if (!string.IsNullOrEmpty(check.Name)) sb.AppendLine($"{indent}    Name: {check.Name}");
                    sb.AppendLine($"{indent}    Condition:");
                    FormatNode(check.Condition, sb, indentLevel + 2);
                    break;

                case LimitClause limit:
                    if (limit.OffsetExpression != null)
                    {
                        sb.AppendLine($"{indent}    OffsetExpression:");
                        FormatNode(limit.OffsetExpression, sb, indentLevel + 2);
                    }
                    if (limit.CountExpression != null)
                    {
                        sb.AppendLine($"{indent}    CountExpression:");
                        FormatNode(limit.CountExpression, sb, indentLevel + 2);
                    }
                    break;

                case GroupByClause groupBy:
                    if (groupBy.WithRollup) sb.AppendLine($"{indent}    WithRollup: True");
                    sb.AppendLine($"{indent}    GroupingExpressions:");
                    foreach (var expr in groupBy.GroupingExpressions)
                        FormatNode(expr, sb, indentLevel + 2);
                    break;

                case SelectExpression selectExpr:
                    sb.AppendLine($"{indent}    Expression:");
                    FormatNode(selectExpr.Expression, sb, indentLevel + 2);
                    if (!string.IsNullOrEmpty(selectExpr.Alias))
                    {
                        var alias = QuoteIdentifier(selectExpr.Alias, selectExpr.AliasQuoteStyle, selectExpr.AliasQuoted);
                        sb.AppendLine($"{indent}    Alias: {alias}");
                    }
                    break;

                case ColumnExpression col:
                    var qualifiedName = "";
                    if (!string.IsNullOrEmpty(col.SchemaName))
                        qualifiedName += QuoteIdentifier(col.SchemaName, col.SchemaQuoteStyle, col.SchemaNameQuoted) + ".";
                    if (!string.IsNullOrEmpty(col.TableAlias))
                        qualifiedName += QuoteIdentifier(col.TableAlias, col.TableAliasQuoteStyle, col.TableAliasQuoted) + ".";
                    qualifiedName += QuoteIdentifier(col.ColumnName, col.ColumnQuoteStyle, col.ColumnNameQuoted);
                    sb.AppendLine($"{indent}    Name: {qualifiedName}");
                    break;

                case TableExpression table:
                    var tableName = "";
                    if (!string.IsNullOrEmpty(table.SchemaName))
                        tableName += QuoteIdentifier(table.SchemaName, table.SchemaQuoteStyle, table.SchemaNameQuoted) + ".";
                    tableName += QuoteIdentifier(table.TableName, table.TableQuoteStyle, table.TableNameQuoted);

                    if (!string.IsNullOrEmpty(table.Alias))
                    {
                        var alias = QuoteIdentifier(table.Alias, table.AliasQuoteStyle, table.AliasQuoted);
                        tableName += $" (Alias: {alias})";
                    }

                    sb.AppendLine($"{indent}    TableName: {tableName}");
                    if (table.Subquery != null)
                        FormatNode(table.Subquery, sb, indentLevel + 1);
                    break;

                case BetweenExpression between:
                    sb.AppendLine($"{indent}    IsNegated: {between.IsNegated}");
                    sb.AppendLine($"{indent}    Expression:");
                    FormatNode(between.Expression, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Lower:");
                    FormatNode(between.Lower, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Upper:");
                    FormatNode(between.Upper, sb, indentLevel + 2);
                    break;

                case AssignmentExpression assignment:
                    sb.AppendLine($"{indent}    Column:");
                    FormatNode(assignment.Column, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Value:");
                    FormatNode(assignment.Value, sb, indentLevel + 2);
                    break;

                case InsertStatement insert:
                    sb.AppendLine($"{indent}    IsReplace: {insert.IsReplace}");
                    sb.AppendLine($"{indent}    IsIgnore: {insert.IsIgnore}");
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(insert.Target, sb, indentLevel + 2);
                    if (insert.Columns.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Columns:");
                        foreach (var col in insert.Columns) FormatNode(col, sb, indentLevel + 2);
                    }
                    if (insert.Values.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Values:");
                        foreach (var row in insert.Values)
                        {
                            sb.AppendLine($"{indent}        Row:");
                            foreach (var val in row) FormatNode(val, sb, indentLevel + 3);
                        }
                    }
                    if (insert.SetAssignments.Count > 0)
                    {
                        sb.AppendLine($"{indent}    SetAssignments:");
                        foreach (var assignment in insert.SetAssignments) FormatNode(assignment, sb, indentLevel + 2);
                    }
                    if (insert.SelectStatement != null)
                    {
                        sb.AppendLine($"{indent}    SelectStatement:");
                        FormatNode(insert.SelectStatement, sb, indentLevel + 2);
                    }
                    if (insert.OnDuplicateKeyUpdate.Count > 0)
                    {
                        sb.AppendLine($"{indent}    OnDuplicateKeyUpdate:");
                        foreach (var assignment in insert.OnDuplicateKeyUpdate) FormatNode(assignment, sb, indentLevel + 2);
                    }
                    break;

                case UpdateStatement update:
                    sb.AppendLine($"{indent}    IsLowPriority: {update.IsLowPriority}");
                    sb.AppendLine($"{indent}    IsIgnore: {update.IsIgnore}");
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(update.Target, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    SetClauses:");
                    foreach (var assignment in update.SetClauses) FormatNode(assignment, sb, indentLevel + 2);
                    if (update.WhereClause != null) FormatNode(update.WhereClause, sb, indentLevel + 1);
                    if (update.OrderByClause != null) FormatNode(update.OrderByClause, sb, indentLevel + 1);
                    if (update.LimitClause != null) FormatNode(update.LimitClause, sb, indentLevel + 1);
                    break;

                case DeleteStatement delete:
                    sb.AppendLine($"{indent}    IsLowPriority: {delete.IsLowPriority}");
                    sb.AppendLine($"{indent}    IsQuick: {delete.IsQuick}");
                    sb.AppendLine($"{indent}    IsIgnore: {delete.IsIgnore}");
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(delete.Target, sb, indentLevel + 2);
                    if (delete.WhereClause != null) FormatNode(delete.WhereClause, sb, indentLevel + 1);
                    if (delete.OrderByClause != null) FormatNode(delete.OrderByClause, sb, indentLevel + 1);
                    if (delete.LimitClause != null) FormatNode(delete.LimitClause, sb, indentLevel + 1);
                    break;

                case FromClause from:
                    sb.AppendLine($"{indent}    TableExpressions:");
                    foreach (var expr in from.TableExpressions)
                    {
                        FormatNode(expr, sb, indentLevel + 2);
                    }
                    break;

                default:
                    var fallback = new SqlAnywhereFormatter().Format(node);
                    sb.Append(fallback);
                    break;
            }
        }

        private static string QuoteIdentifier(string identifier, QuoteStyle quoteStyle, bool quotedFallback)
        {
            if (quoteStyle == QuoteStyle.None && quotedFallback)
            {
                quoteStyle = QuoteStyle.Backtick;
            }

            if (quoteStyle == QuoteStyle.Backtick)
            {
                return "`" + identifier.Replace("`", "``") + "`";
            }

            if (quoteStyle == QuoteStyle.DoubleQuote)
            {
                return "\"" + identifier.Replace("\"", "\"\"") + "\"";
            }

            return identifier;
        }
    }
}
