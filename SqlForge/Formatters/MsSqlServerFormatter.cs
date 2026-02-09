using System.Text;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Formatters
{
    /// <summary>
    /// Implements ISqlFormatter for MS SQL Server dialect, producing a readable AST representation.
    /// </summary>
    public class MsSqlServerFormatter : BaseSqlFormatter
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
                    if (select.TopClause != null) FormatNode(select.TopClause, sb, indentLevel + 1);
                    sb.AppendLine($"{indent}    SelectItems:");
                    foreach (var item in select.SelectItems) FormatNode(item, sb, indentLevel + 2);
                    if (select.FromClause != null) FormatNode(select.FromClause, sb, indentLevel + 1);
                    if (select.WhereClause != null) FormatNode(select.WhereClause, sb, indentLevel + 1);
                    if (select.GroupByClause != null) FormatNode(select.GroupByClause, sb, indentLevel + 1);
                    if (select.HavingClause != null) FormatNode(select.HavingClause, sb, indentLevel + 1);
                    if (select.OrderByClause != null) FormatNode(select.OrderByClause, sb, indentLevel + 1);
                    if (select.OffsetFetchClause != null) FormatNode(select.OffsetFetchClause, sb, indentLevel + 1);
                    break;

                case TopClause top:
                    sb.AppendLine($"{indent}    IsPercent: {top.IsPercent}");
                    sb.AppendLine($"{indent}    WithTies: {top.WithTies}");
                    sb.AppendLine($"{indent}    Expression:");
                    FormatNode(top.Expression, sb, indentLevel + 2);
                    break;

                case OffsetFetchClause offset:
                    sb.AppendLine($"{indent}    IsNext: {offset.IsNext}");
                    sb.AppendLine($"{indent}    OffsetExpression:");
                    FormatNode(offset.OffsetExpression, sb, indentLevel + 2);
                    if (offset.FetchExpression != null)
                    {
                        sb.AppendLine($"{indent}    FetchExpression:");
                        FormatNode(offset.FetchExpression, sb, indentLevel + 2);
                    }
                    break;

                case TableHint hint:
                    sb.AppendLine($"{indent}    HintName: {hint.HintName}");
                    if (hint.Parameters.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Parameters:");
                        foreach (var p in hint.Parameters) FormatNode(p, sb, indentLevel + 2);
                    }
                    break;

                case WindowFunctionExpression window:
                    sb.AppendLine($"{indent}    FunctionName: {window.FunctionName}");
                    break;

                default:
                    // Reuse SQL Anywhere formatting for all shared node types.
                    var fallback = new SqlAnywhereFormatter().Format(node);
                    sb.Append(fallback);
                    break;
            }
        }
    }
}
