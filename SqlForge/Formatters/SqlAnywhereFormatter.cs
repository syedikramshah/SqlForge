using System.Text;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Formatters
{
    /// <summary>
    /// Implements ISqlFormatter for SQL Anywhere dialect, producing a human-readable AST representation.
    /// </summary>
    public class SqlAnywhereFormatter : BaseSqlFormatter
    {
        public override string Format(ISqlNode node)
        {
            var sb = new StringBuilder();
            FormatNode(node, sb, 0);
            return sb.ToString();
        }

        /// <summary>
        /// Recursively formats an AST node and appends its representation to the StringBuilder.
        /// </summary>
        private void FormatNode(ISqlNode node, StringBuilder sb, int indentLevel)
        {
            if (node == null) return;

            var indent = new string(' ', indentLevel * 4);
            sb.AppendLine($"{indent}{node.GetType().Name}:");

            switch (node)
            {
                case SqlStatement stmt:
                    sb.AppendLine($"{indent}    Type: {stmt.Type}");
                    FormatNode(stmt.Body, sb, indentLevel + 1);
                    break;
                case SelectStatement select:
                    if (select.IsDistinct) sb.AppendLine($"{indent}    IsDistinct: True");
                    sb.AppendLine($"{indent}    SelectItems:");
                    foreach (var item in select.SelectItems)
                        FormatNode(item, sb, indentLevel + 2);
                    if (select.FromClause != null)
                        FormatNode(select.FromClause, sb, indentLevel + 1);
                    if (select.WhereClause != null)
                        FormatNode(select.WhereClause, sb, indentLevel + 1);
                    if (select.GroupByClause != null)
                        FormatNode(select.GroupByClause, sb, indentLevel + 1);
                    if (select.HavingClause != null)
                        FormatNode(select.HavingClause, sb, indentLevel + 1);
                    if (select.OrderByClause != null)
                        FormatNode(select.OrderByClause, sb, indentLevel + 1);
                    break;
                case SelectExpression selectExpr:
                    sb.AppendLine($"{indent}    Expression:");
                    FormatNode(selectExpr.Expression, sb, indentLevel + 2);
                    if (!string.IsNullOrEmpty(selectExpr.Alias))
                        sb.AppendLine($"{indent}    Alias: {selectExpr.Alias}");
                    break;
                case ColumnExpression col:
                    string qualifiedName = "";
                    if (!string.IsNullOrEmpty(col.SchemaName)) qualifiedName += col.SchemaName + ".";
                    if (!string.IsNullOrEmpty(col.TableAlias)) qualifiedName += col.TableAlias + ".";
                    qualifiedName += col.ColumnName;
                    sb.AppendLine($"{indent}    Name: {qualifiedName}"); // Alias is now in SelectExpression
                    break;
                case FromClause from:
                    sb.AppendLine($"{indent}    TableExpressions:");
                    foreach (var expr in from.TableExpressions)
                        FormatNode(expr, sb, indentLevel + 2);
                    break;
                case TableExpression table:
                    string tableName = "";
                    if (!string.IsNullOrEmpty(table.SchemaName)) tableName += table.SchemaName + ".";
                    tableName += table.TableName;
                    sb.AppendLine($"{indent}    TableName: {tableName}" + (string.IsNullOrEmpty(table.Alias) ? "" : $" (Alias: {table.Alias})"));
                    if (table.Subquery != null)
                        FormatNode(table.Subquery, sb, indentLevel + 1);
                    break;
                case JoinExpression join:
                    sb.AppendLine($"{indent}    JoinType: {join.Type}");
                    sb.AppendLine($"{indent}    Left:");
                    FormatNode(join.Left, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Right:");
                    FormatNode(join.Right, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    OnCondition:");
                    FormatNode(join.OnCondition, sb, indentLevel + 2);
                    break;
                case WhereClause where:
                    sb.AppendLine($"{indent}    Condition:");
                    FormatNode(where.Condition, sb, indentLevel + 2);
                    break;
                case GroupByClause groupBy:
                    sb.AppendLine($"{indent}    GroupingExpressions:");
                    foreach (var expr in groupBy.GroupingExpressions)
                        FormatNode(expr, sb, indentLevel + 2);
                    break;
                case HavingClause having:
                    sb.AppendLine($"{indent}    Condition:");
                    FormatNode(having.Condition, sb, indentLevel + 2);
                    break;
                case OrderByClause orderBy:
                    sb.AppendLine($"{indent}    OrderItems:");
                    foreach (var item in orderBy.OrderItems)
                        FormatNode(item, sb, indentLevel + 2);
                    break;
                case OrderItem orderItem:
                    sb.AppendLine($"{indent}    Expression:");
                    FormatNode(orderItem.Expression, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Direction: {(orderItem.IsAscending ? "ASC" : "DESC")}");
                    break;
                case SubqueryExpression subquery:
                    sb.AppendLine($"{indent}    Subquery:");
                    FormatNode(subquery.SubqueryStatement, sb, indentLevel + 2);
                    if (!string.IsNullOrEmpty(subquery.Alias))
                        sb.AppendLine($"{indent}    Alias: {subquery.Alias}");
                    break;
                case BinaryExpression bin:
                    sb.AppendLine($"{indent}    Left:");
                    FormatNode(bin.Left, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Operator: {bin.Operator}");
                    sb.AppendLine($"{indent}    Right:");
                    FormatNode(bin.Right, sb, indentLevel + 2);
                    break;
                case UnaryExpression unary:
                    sb.AppendLine($"{indent}    Operator: {unary.Operator}");
                    sb.AppendLine($"{indent}    Expression:");
                    FormatNode(unary.Expression, sb, indentLevel + 2);
                    break;
                case LiteralExpression literal:
                    sb.AppendLine($"{indent}    Value: {literal.Value} (Type: {literal.Type})");
                    break;
                case FunctionCallExpression func:
                    sb.AppendLine($"{indent}    FunctionName: {func.FunctionName}");
                    if (func.IsAllColumns) sb.AppendLine($"{indent}    Arguments: *");
                    else
                    {
                        sb.AppendLine($"{indent}    Arguments:");
                        foreach (var arg in func.Arguments)
                            FormatNode(arg, sb, indentLevel + 2);
                    }
                    break;
                default:
                    sb.AppendLine($"{indent}    (Unknown Node Type: {node.GetType().Name})");
                    break;
            }
        }
    }
}