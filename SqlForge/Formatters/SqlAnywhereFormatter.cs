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
                    if (select.LimitClause != null)
                        FormatNode(select.LimitClause, sb, indentLevel + 1);
                    break;
                case InsertStatement insert:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(insert.Target, sb, indentLevel + 2);
                    if (insert.Columns.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Columns:");
                        foreach (var col in insert.Columns)
                            FormatNode(col, sb, indentLevel + 2);
                    }
                    if (insert.Values.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Values:");
                        foreach (var row in insert.Values)
                        {
                            sb.AppendLine($"{indent}        Row:");
                            foreach (var val in row)
                                FormatNode(val, sb, indentLevel + 3);
                        }
                    }
                    if (insert.SelectStatement != null)
                    {
                        sb.AppendLine($"{indent}    SelectStatement:");
                        FormatNode(insert.SelectStatement, sb, indentLevel + 2);
                    }
                    break;
                case UpdateStatement update:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(update.Target, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    SetClauses:");
                    foreach (var assignment in update.SetClauses)
                        FormatNode(assignment, sb, indentLevel + 2);
                    if (update.WhereClause != null)
                        FormatNode(update.WhereClause, sb, indentLevel + 1);
                    break;
                case DeleteStatement delete:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(delete.Target, sb, indentLevel + 2);
                    if (delete.WhereClause != null)
                        FormatNode(delete.WhereClause, sb, indentLevel + 1);
                    break;
                case CreateTableStatement create:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(create.Target, sb, indentLevel + 2);
                    if (create.Columns.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Columns:");
                        foreach (var col in create.Columns)
                            FormatNode(col, sb, indentLevel + 2);
                    }
                    if (create.Constraints.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Constraints:");
                        foreach (var constraint in create.Constraints)
                            FormatNode(constraint, sb, indentLevel + 2);
                    }
                    break;
                case CreateIndexStatement createIndex:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(createIndex.Target, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Index:");
                    FormatNode(createIndex.Index, sb, indentLevel + 2);
                    if (createIndex.IncludeColumns.Count > 0)
                    {
                        sb.AppendLine($"{indent}    IncludeColumns:");
                        foreach (var col in createIndex.IncludeColumns)
                            FormatNode(col, sb, indentLevel + 2);
                    }
                    break;
                case DropIndexStatement dropIndex:
                    sb.AppendLine($"{indent}    IndexName: {dropIndex.IndexName}");
                    sb.AppendLine($"{indent}    IfExists: {dropIndex.IfExists}");
                    if (dropIndex.Target != null)
                    {
                        sb.AppendLine($"{indent}    Target:");
                        FormatNode(dropIndex.Target, sb, indentLevel + 2);
                    }
                    break;
                case DropTableStatement drop:
                    sb.AppendLine($"{indent}    IfExists: {drop.IfExists}");
                    sb.AppendLine($"{indent}    Targets:");
                    foreach (var target in drop.Targets)
                        FormatNode(target, sb, indentLevel + 2);
                    break;
                case AlterTableStatement alter:
                    sb.AppendLine($"{indent}    Target:");
                    FormatNode(alter.Target, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Actions:");
                    foreach (var action in alter.Actions)
                        FormatNode(action, sb, indentLevel + 2);
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
                case AssignmentExpression assignment:
                    sb.AppendLine($"{indent}    Column:");
                    FormatNode(assignment.Column, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    Value:");
                    FormatNode(assignment.Value, sb, indentLevel + 2);
                    break;
                case ColumnDefinition column:
                    sb.AppendLine($"{indent}    Name: {column.Name}");
                    sb.AppendLine($"{indent}    DataType: {column.DataType}");
                    if (column.IsNullable.HasValue)
                        sb.AppendLine($"{indent}    IsNullable: {column.IsNullable.Value}");
                    if (column.IsIdentity)
                        sb.AppendLine($"{indent}    IsIdentity: True");
                    if (column.IdentitySeed != null)
                    {
                        sb.AppendLine($"{indent}    IdentitySeed:");
                        FormatNode(column.IdentitySeed, sb, indentLevel + 2);
                    }
                    if (column.IdentityIncrement != null)
                    {
                        sb.AppendLine($"{indent}    IdentityIncrement:");
                        FormatNode(column.IdentityIncrement, sb, indentLevel + 2);
                    }
                    if (column.IsPrimaryKey)
                        sb.AppendLine($"{indent}    IsPrimaryKey: True");
                    if (column.IsUnique)
                        sb.AppendLine($"{indent}    IsUnique: True");
                    if (column.DefaultExpression != null)
                    {
                        sb.AppendLine($"{indent}    DefaultExpression:");
                        FormatNode(column.DefaultExpression, sb, indentLevel + 2);
                    }
                    break;
                case TableConstraint constraint:
                    sb.AppendLine($"{indent}    Type: {constraint.Type}");
                    if (!string.IsNullOrEmpty(constraint.Name))
                        sb.AppendLine($"{indent}    Name: {constraint.Name}");
                    if (constraint.Columns.Count > 0)
                    {
                        sb.AppendLine($"{indent}    Columns:");
                        foreach (var col in constraint.Columns)
                            FormatNode(col, sb, indentLevel + 2);
                    }
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
                case ForeignKeyConstraint foreignKey:
                    if (!string.IsNullOrEmpty(foreignKey.Name))
                        sb.AppendLine($"{indent}    Name: {foreignKey.Name}");
                    sb.AppendLine($"{indent}    Columns:");
                    foreach (var col in foreignKey.Columns)
                        FormatNode(col, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    ReferencedTable:");
                    FormatNode(foreignKey.ReferencedTable, sb, indentLevel + 2);
                    sb.AppendLine($"{indent}    ReferencedColumns:");
                    foreach (var col in foreignKey.ReferencedColumns)
                        FormatNode(col, sb, indentLevel + 2);
                    if (foreignKey.OnDelete.HasValue)
                        sb.AppendLine($"{indent}    OnDelete: {foreignKey.OnDelete}");
                    if (foreignKey.OnUpdate.HasValue)
                        sb.AppendLine($"{indent}    OnUpdate: {foreignKey.OnUpdate}");
                    break;
                case CheckConstraint check:
                    if (!string.IsNullOrEmpty(check.Name))
                        sb.AppendLine($"{indent}    Name: {check.Name}");
                    sb.AppendLine($"{indent}    Condition:");
                    FormatNode(check.Condition, sb, indentLevel + 2);
                    break;
                case AlterTableAction action:
                    sb.AppendLine($"{indent}    ActionType: {action.ActionType}");
                    if (action.Column != null)
                    {
                        sb.AppendLine($"{indent}    Column:");
                        FormatNode(action.Column, sb, indentLevel + 2);
                    }
                    if (action.Constraint != null)
                    {
                        sb.AppendLine($"{indent}    Constraint:");
                        FormatNode(action.Constraint, sb, indentLevel + 2);
                    }
                    if (!string.IsNullOrEmpty(action.ColumnName))
                        sb.AppendLine($"{indent}    ColumnName: {action.ColumnName}");
                    if (!string.IsNullOrEmpty(action.NewTableName))
                        sb.AppendLine($"{indent}    NewTableName: {action.NewTableName}");
                    if (!string.IsNullOrEmpty(action.NewColumnName))
                        sb.AppendLine($"{indent}    NewColumnName: {action.NewColumnName}");
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
                case InExpression inExpr:
                    sb.AppendLine($"{indent}    IsNegated: {inExpr.IsNegated}");
                    sb.AppendLine($"{indent}    Expression:");
                    FormatNode(inExpr.Expression, sb, indentLevel + 2);
                    if (inExpr.Subquery != null)
                    {
                        sb.AppendLine($"{indent}    Subquery:");
                        FormatNode(inExpr.Subquery, sb, indentLevel + 2);
                    }
                    else
                    {
                        sb.AppendLine($"{indent}    Values:");
                        foreach (var value in inExpr.Values)
                            FormatNode(value, sb, indentLevel + 2);
                    }
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