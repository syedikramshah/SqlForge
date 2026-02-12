using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using SqlForge.Enums;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Reconstructors
{
    public class MsSqlServerReconstructor : BaseSqlReconstructor
    {
        private StringBuilder _sb;

        public override string Reconstruct(SqlStatement statement, SqlDialect dialect = SqlDialect.MsSqlServer)
        {
            _sb = new StringBuilder();
            ReconstructNode(statement);
            if (!_sb.ToString().TrimEnd().EndsWith(";"))
            {
                _sb.Append(";");
            }

            return _sb.ToString();
        }

        private void ReconstructNode(ISqlNode node)
        {
            if (node == null)
            {
                return;
            }

            switch (node)
            {
                case SqlStatement stmt:
                    if (stmt.WithClause != null)
                    {
                        ReconstructNode(stmt.WithClause);
                        _sb.Append(" ");
                    }
                    ReconstructNode(stmt.Body);
                    break;

                case WithClause withClause:
                    _sb.Append("WITH ");
                    for (int i = 0; i < withClause.CommonTableExpressions.Count; i++)
                    {
                        ReconstructNode(withClause.CommonTableExpressions[i]);
                        if (i < withClause.CommonTableExpressions.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }
                    break;

                case CommonTableExpression cte:
                    _sb.Append(QuoteIdentifier(cte.Name, QuoteStyle.SquareBracket));
                    if (cte.ColumnNames.Any())
                    {
                        _sb.Append(" (");
                        for (int i = 0; i < cte.ColumnNames.Count; i++)
                        {
                            _sb.Append(QuoteIdentifier(cte.ColumnNames[i], QuoteStyle.SquareBracket));
                            if (i < cte.ColumnNames.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }
                        _sb.Append(")");
                    }
                    _sb.Append(" AS (");
                    ReconstructNode(cte.Query);
                    _sb.Append(")");
                    break;

                case SelectStatement select:
                    _sb.Append("SELECT ");
                    if (select.IsDistinct)
                    {
                        _sb.Append("DISTINCT ");
                    }

                    if (select.TopClause != null)
                    {
                        ReconstructNode(select.TopClause);
                        _sb.Append(" ");
                    }

                    for (int i = 0; i < select.SelectItems.Count; i++)
                    {
                        ReconstructNode(select.SelectItems[i]);
                        if (i < select.SelectItems.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }

                    if (select.FromClause != null)
                    {
                        _sb.Append(" FROM ");
                        ReconstructNode(select.FromClause);
                    }

                    if (select.WhereClause != null)
                    {
                        _sb.Append(" WHERE ");
                        ReconstructNode(select.WhereClause);
                    }

                    if (select.GroupByClause != null)
                    {
                        _sb.Append(" GROUP BY ");
                        ReconstructNode(select.GroupByClause);
                    }

                    if (select.HavingClause != null)
                    {
                        _sb.Append(" HAVING ");
                        ReconstructNode(select.HavingClause);
                    }

                    if (select.OrderByClause != null)
                    {
                        _sb.Append(" ORDER BY ");
                        ReconstructNode(select.OrderByClause);
                    }

                    if (select.OffsetFetchClause != null)
                    {
                        _sb.Append(" ");
                        ReconstructNode(select.OffsetFetchClause);
                    }
                    break;

                case InsertStatement insert:
                    _sb.Append("INSERT INTO ");
                    ReconstructNode(insert.Target);
                    if (insert.Columns.Any())
                    {
                        _sb.Append(" (");
                        for (int i = 0; i < insert.Columns.Count; i++)
                        {
                            ReconstructNode(insert.Columns[i]);
                            if (i < insert.Columns.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }
                        _sb.Append(")");
                    }

                    if (insert.Values.Any())
                    {
                        _sb.Append(" VALUES ");
                        for (int i = 0; i < insert.Values.Count; i++)
                        {
                            _sb.Append("(");
                            for (int j = 0; j < insert.Values[i].Count; j++)
                            {
                                ReconstructNode(insert.Values[i][j]);
                                if (j < insert.Values[i].Count - 1)
                                {
                                    _sb.Append(", ");
                                }
                            }
                            _sb.Append(")");
                            if (i < insert.Values.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }
                    }
                    else if (insert.SelectStatement != null)
                    {
                        _sb.Append(" ");
                        ReconstructNode(insert.SelectStatement);
                    }
                    break;

                case UpdateStatement update:
                    _sb.Append("UPDATE ");
                    ReconstructNode(update.Target);
                    _sb.Append(" SET ");
                    for (int i = 0; i < update.SetClauses.Count; i++)
                    {
                        ReconstructNode(update.SetClauses[i]);
                        if (i < update.SetClauses.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }
                    if (update.WhereClause != null)
                    {
                        _sb.Append(" WHERE ");
                        ReconstructNode(update.WhereClause);
                    }
                    break;

                case DeleteStatement delete:
                    _sb.Append("DELETE FROM ");
                    ReconstructNode(delete.Target);
                    if (delete.WhereClause != null)
                    {
                        _sb.Append(" WHERE ");
                        ReconstructNode(delete.WhereClause);
                    }
                    break;

                case CreateTableStatement create:
                    _sb.Append("CREATE TABLE ");
                    ReconstructNode(create.Target);
                    if (create.Columns.Any() || create.Constraints.Any())
                    {
                        _sb.Append(" (");
                        bool needsComma = false;
                        for (int i = 0; i < create.Columns.Count; i++)
                        {
                            if (needsComma) _sb.Append(", ");
                            ReconstructNode(create.Columns[i]);
                            needsComma = true;
                        }
                        for (int i = 0; i < create.Constraints.Count; i++)
                        {
                            if (needsComma) _sb.Append(", ");
                            ReconstructNode(create.Constraints[i]);
                            needsComma = true;
                        }
                        _sb.Append(")");
                    }
                    break;

                case CreateIndexStatement createIndex:
                    _sb.Append("CREATE ");
                    if (createIndex.Index.Type == IndexType.Unique)
                    {
                        _sb.Append("UNIQUE ");
                    }
                    _sb.Append("INDEX ");
                    _sb.Append(QuoteIdentifier(createIndex.Index.Name, createIndex.Index.NameQuoteStyle, createIndex.Index.NameQuoted));
                    _sb.Append(" ON ");
                    ReconstructNode(createIndex.Target);
                    _sb.Append(" ");
                    ReconstructIndexColumns(createIndex.Index.Columns);
                    if (createIndex.IncludeColumns.Any())
                    {
                        _sb.Append(" INCLUDE ");
                        ReconstructColumnList(createIndex.IncludeColumns);
                    }
                    break;

                case DropTableStatement drop:
                    _sb.Append("DROP TABLE ");
                    if (drop.IfExists)
                    {
                        _sb.Append("IF EXISTS ");
                    }
                    for (int i = 0; i < drop.Targets.Count; i++)
                    {
                        ReconstructNode(drop.Targets[i]);
                        if (i < drop.Targets.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }
                    break;

                case DropIndexStatement dropIndex:
                    _sb.Append("DROP INDEX ");
                    if (dropIndex.IfExists)
                    {
                        _sb.Append("IF EXISTS ");
                    }
                    _sb.Append(QuoteIdentifier(dropIndex.IndexName, dropIndex.IndexNameQuoteStyle, dropIndex.IndexNameQuoted));
                    _sb.Append(" ON ");
                    ReconstructNode(dropIndex.Target);
                    break;

                case AlterTableStatement alter:
                    _sb.Append("ALTER TABLE ");
                    ReconstructNode(alter.Target);
                    _sb.Append(" ");
                    for (int i = 0; i < alter.Actions.Count; i++)
                    {
                        ReconstructNode(alter.Actions[i]);
                        if (i < alter.Actions.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }
                    break;

                case TopClause top:
                    _sb.Append("TOP ");
                    ReconstructNode(top.Expression);
                    if (top.IsPercent)
                    {
                        _sb.Append(" PERCENT");
                    }
                    if (top.WithTies)
                    {
                        _sb.Append(" WITH TIES");
                    }
                    break;

                case OffsetFetchClause offsetFetch:
                    _sb.Append("OFFSET ");
                    ReconstructNode(offsetFetch.OffsetExpression);
                    _sb.Append(" ROWS");
                    if (offsetFetch.FetchExpression != null)
                    {
                        _sb.Append(offsetFetch.IsNext ? " FETCH NEXT " : " FETCH FIRST ");
                        ReconstructNode(offsetFetch.FetchExpression);
                        _sb.Append(" ROWS ONLY");
                    }
                    break;

                case SetOperatorExpression setExpr:
                    _sb.Append("(");
                    ReconstructNode(setExpr.Left);
                    _sb.Append(" ");
                    switch (setExpr.Operator)
                    {
                        case SetOperatorType.Union:
                            _sb.Append("UNION");
                            break;
                        case SetOperatorType.UnionAll:
                            _sb.Append("UNION ALL");
                            break;
                        case SetOperatorType.Intersect:
                            _sb.Append("INTERSECT");
                            break;
                        case SetOperatorType.Except:
                            _sb.Append("EXCEPT");
                            break;
                    }
                    _sb.Append(" ");
                    ReconstructNode(setExpr.Right);
                    _sb.Append(")");
                    break;

                case SelectExpression selectExpr:
                    ReconstructNode(selectExpr.Expression);
                    if (!string.IsNullOrEmpty(selectExpr.Alias))
                    {
                        _sb.Append(selectExpr.HasExplicitAs ? " AS " : " ");
                        _sb.Append(QuoteIdentifier(selectExpr.Alias, selectExpr.AliasQuoteStyle, selectExpr.AliasQuoted));
                    }
                    break;

                case ColumnExpression col:
                    if (!string.IsNullOrEmpty(col.SchemaName))
                    {
                        _sb.Append(QuoteIdentifier(col.SchemaName, col.SchemaQuoteStyle, col.SchemaNameQuoted));
                        _sb.Append(".");
                    }
                    if (!string.IsNullOrEmpty(col.TableAlias))
                    {
                        _sb.Append(QuoteIdentifier(col.TableAlias, col.TableAliasQuoteStyle, col.TableAliasQuoted));
                        _sb.Append(".");
                    }
                    _sb.Append(QuoteIdentifier(col.ColumnName, col.ColumnQuoteStyle, col.ColumnNameQuoted));
                    break;

                case FromClause from:
                    if (from.TableExpressions != null && from.TableExpressions.Any())
                    {
                        ReconstructNode(from.TableExpressions.First());
                    }
                    break;

                case TableExpression table:
                    if (!string.IsNullOrEmpty(table.SchemaName))
                    {
                        _sb.Append(QuoteIdentifier(table.SchemaName, table.SchemaQuoteStyle, table.SchemaNameQuoted));
                        _sb.Append(".");
                    }
                    _sb.Append(QuoteIdentifier(table.TableName, table.TableQuoteStyle, table.TableNameQuoted));

                    if (table.TableHints != null && table.TableHints.Any())
                    {
                        _sb.Append(" WITH (");
                        for (int i = 0; i < table.TableHints.Count; i++)
                        {
                            ReconstructNode(table.TableHints[i]);
                            if (i < table.TableHints.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }
                        _sb.Append(")");
                    }

                    if (!string.IsNullOrEmpty(table.Alias))
                    {
                        _sb.Append(table.HasExplicitAs ? " AS " : " ");
                        _sb.Append(QuoteIdentifier(table.Alias, table.AliasQuoteStyle, table.AliasQuoted));
                    }
                    break;

                case TableHint hint:
                    _sb.Append(hint.HintName);
                    if (hint.Parameters != null && hint.Parameters.Any())
                    {
                        _sb.Append("(");
                        for (int i = 0; i < hint.Parameters.Count; i++)
                        {
                            ReconstructNode(hint.Parameters[i]);
                            if (i < hint.Parameters.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }
                        _sb.Append(")");
                    }
                    break;

                case JoinExpression join:
                    {
                        bool leftNeedsParens = join.Left is JoinExpression || join.Left is SubqueryExpression;
                        bool rightNeedsParens = join.Right is JoinExpression || join.Right is SubqueryExpression;
                        if ((join.Type == JoinType.CrossApply || join.Type == JoinType.OuterApply) && join.Right is SubqueryExpression)
                        {
                            rightNeedsParens = false;
                        }

                        if (leftNeedsParens) _sb.Append("(");
                        ReconstructNode(join.Left);
                        if (leftNeedsParens) _sb.Append(")");
                        _sb.Append(" ");

                        string joinText;
                        switch (join.Type)
                        {
                            case JoinType.Inner:
                                joinText = "INNER JOIN";
                                break;
                            case JoinType.Left:
                                joinText = "LEFT OUTER JOIN";
                                break;
                            case JoinType.Right:
                                joinText = "RIGHT OUTER JOIN";
                                break;
                            case JoinType.Full:
                                joinText = "FULL OUTER JOIN";
                                break;
                            case JoinType.Cross:
                                joinText = "CROSS JOIN";
                                break;
                            case JoinType.Natural:
                                joinText = "NATURAL JOIN";
                                break;
                            case JoinType.CrossApply:
                                joinText = "CROSS APPLY";
                                break;
                            case JoinType.OuterApply:
                                joinText = "OUTER APPLY";
                                break;
                            default:
                                joinText = join.Type.ToString().ToUpperInvariant() + " JOIN";
                                break;
                        }

                        _sb.Append(joinText);
                        _sb.Append(" ");
                        if (rightNeedsParens) _sb.Append("(");
                        ReconstructNode(join.Right);
                        if (rightNeedsParens) _sb.Append(")");

                        if (join.OnCondition != null)
                        {
                            _sb.Append(" ON ");
                            ReconstructNode(join.OnCondition);
                        }
                    }
                    break;

                case WhereClause where:
                    ReconstructNode(where.Condition);
                    break;

                case GroupByClause groupBy:
                    for (int i = 0; i < groupBy.GroupingExpressions.Count; i++)
                    {
                        ReconstructNode(groupBy.GroupingExpressions[i]);
                        if (i < groupBy.GroupingExpressions.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }
                    break;

                case HavingClause having:
                    ReconstructNode(having.Condition);
                    break;

                case OrderByClause orderBy:
                    for (int i = 0; i < orderBy.OrderItems.Count; i++)
                    {
                        ReconstructNode(orderBy.OrderItems[i]);
                        if (i < orderBy.OrderItems.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }
                    break;

                case OrderItem orderItem:
                    ReconstructNode(orderItem.Expression);
                    _sb.Append(orderItem.IsAscending ? " ASC" : " DESC");
                    break;

                case AssignmentExpression assignment:
                    ReconstructNode(assignment.Column);
                    _sb.Append(" = ");
                    ReconstructNode(assignment.Value);
                    break;

                case ColumnDefinition column:
                    _sb.Append(QuoteIdentifier(column.Name, column.NameQuoteStyle, column.NameQuoted));
                    _sb.Append(" ");
                    _sb.Append(column.DataType);
                    if (column.IsIdentity)
                    {
                        _sb.Append(" IDENTITY");
                        if (column.IdentitySeed != null || column.IdentityIncrement != null)
                        {
                            _sb.Append("(");
                            if (column.IdentitySeed != null)
                            {
                                ReconstructNode(column.IdentitySeed);
                            }
                            if (column.IdentityIncrement != null)
                            {
                                _sb.Append(", ");
                                ReconstructNode(column.IdentityIncrement);
                            }
                            _sb.Append(")");
                        }
                    }
                    if (column.IsNullable.HasValue)
                    {
                        _sb.Append(column.IsNullable.Value ? " NULL" : " NOT NULL");
                    }
                    if (column.DefaultExpression != null)
                    {
                        _sb.Append(" DEFAULT ");
                        ReconstructNode(column.DefaultExpression);
                    }
                    if (column.IsPrimaryKey)
                    {
                        _sb.Append(" PRIMARY KEY");
                    }
                    if (column.IsUnique)
                    {
                        _sb.Append(" UNIQUE");
                    }
                    break;

                case TableConstraint constraint:
                    if (constraint.Type == ConstraintType.PrimaryKey)
                    {
                        if (!string.IsNullOrEmpty(constraint.Name))
                        {
                            _sb.Append("CONSTRAINT ");
                            _sb.Append(QuoteIdentifier(constraint.Name, constraint.NameQuoteStyle, constraint.NameQuoted));
                            _sb.Append(" ");
                        }
                        _sb.Append("PRIMARY KEY ");
                        ReconstructColumnList(constraint.Columns);
                    }
                    else if (constraint.Type == ConstraintType.UniqueKey)
                    {
                        if (!string.IsNullOrEmpty(constraint.Name))
                        {
                            _sb.Append("CONSTRAINT ");
                            _sb.Append(QuoteIdentifier(constraint.Name, constraint.NameQuoteStyle, constraint.NameQuoted));
                            _sb.Append(" ");
                        }
                        _sb.Append("UNIQUE ");
                        ReconstructColumnList(constraint.Columns);
                    }
                    break;

                case ForeignKeyConstraint foreignKey:
                    if (!string.IsNullOrEmpty(foreignKey.Name))
                    {
                        _sb.Append("CONSTRAINT ");
                        _sb.Append(QuoteIdentifier(foreignKey.Name, foreignKey.NameQuoteStyle, foreignKey.NameQuoted));
                        _sb.Append(" ");
                    }
                    _sb.Append("FOREIGN KEY ");
                    ReconstructColumnList(foreignKey.Columns);
                    _sb.Append(" REFERENCES ");
                    ReconstructNode(foreignKey.ReferencedTable);
                    ReconstructColumnList(foreignKey.ReferencedColumns);
                    AppendReferentialActions(foreignKey);
                    break;

                case CheckConstraint check:
                    if (!string.IsNullOrEmpty(check.Name))
                    {
                        _sb.Append("CONSTRAINT ");
                        _sb.Append(QuoteIdentifier(check.Name, check.NameQuoteStyle, check.NameQuoted));
                        _sb.Append(" ");
                    }
                    _sb.Append("CHECK (");
                    ReconstructNode(check.Condition);
                    _sb.Append(")");
                    break;

                case AlterTableAction action:
                    switch (action.ActionType)
                    {
                        case AlterTableActionType.AddColumn:
                            _sb.Append("ADD ");
                            ReconstructNode(action.Column);
                            break;
                        case AlterTableActionType.AddConstraint:
                            _sb.Append("ADD ");
                            ReconstructNode(action.Constraint);
                            break;
                        case AlterTableActionType.DropColumn:
                            _sb.Append("DROP COLUMN ");
                            _sb.Append(QuoteIdentifier(action.ColumnName, action.ColumnNameQuoteStyle, action.ColumnNameQuoted));
                            break;
                        case AlterTableActionType.AlterColumn:
                            _sb.Append("ALTER COLUMN ");
                            ReconstructNode(action.Column);
                            break;
                        case AlterTableActionType.RenameTo:
                            _sb.Append("RENAME TO ");
                            _sb.Append(QuoteIdentifier(action.NewTableName, action.NewTableNameQuoteStyle, action.NewTableNameQuoted));
                            break;
                        case AlterTableActionType.RenameColumn:
                            _sb.Append("RENAME COLUMN ");
                            _sb.Append(QuoteIdentifier(action.ColumnName, action.ColumnNameQuoteStyle, action.ColumnNameQuoted));
                            _sb.Append(" TO ");
                            _sb.Append(QuoteIdentifier(action.NewColumnName, action.NewColumnNameQuoteStyle, action.NewColumnNameQuoted));
                            break;
                    }
                    break;

                case SubqueryExpression subquery:
                    {
                        bool contentIsSetOperator = subquery.SubqueryStatement != null && subquery.SubqueryStatement.Body is SetOperatorExpression;
                        if (!contentIsSetOperator)
                        {
                            _sb.Append("(");
                        }

                        ReconstructNode(subquery.SubqueryStatement);

                        if (!contentIsSetOperator)
                        {
                            _sb.Append(")");
                        }

                        if (!string.IsNullOrEmpty(subquery.Alias))
                        {
                            _sb.Append(" AS ");
                            _sb.Append(QuoteIdentifier(subquery.Alias, subquery.AliasQuoteStyle, subquery.AliasQuoted));
                        }
                    }
                    break;

                case InExpression inExpr:
                    ReconstructNode(inExpr.Expression);
                    _sb.Append(inExpr.IsNegated ? " NOT IN " : " IN ");

                    if (inExpr.Subquery != null)
                    {
                        if (inExpr.Subquery.Body is SetOperatorExpression)
                        {
                            ReconstructNode(inExpr.Subquery.Body);
                        }
                        else
                        {
                            _sb.Append("(");
                            ReconstructNode(inExpr.Subquery);
                            _sb.Append(")");
                        }
                    }
                    else
                    {
                        _sb.Append("(");
                        for (int i = 0; i < inExpr.Values.Count; i++)
                        {
                            ReconstructNode(inExpr.Values[i]);
                            if (i < inExpr.Values.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }
                        _sb.Append(")");
                    }
                    break;

                case BinaryExpression bin:
                    {
                        string op = bin.Operator.ToUpperInvariant();
                        bool leftNeedsParens = NeedsParenForPrecedence(op, bin.Left);
                        bool rightNeedsParens = NeedsParenForPrecedence(op, bin.Right);

                        if (leftNeedsParens) _sb.Append("(");
                        ReconstructNode(bin.Left);
                        if (leftNeedsParens) _sb.Append(")");

                        _sb.Append($" {op} ");

                        if (op == "IN")
                        {
                            if (bin.Right is FunctionCallExpression func && func.FunctionName == "IN_LIST")
                            {
                                _sb.Append("(");
                                for (int i = 0; i < func.Arguments.Count; i++)
                                {
                                    ReconstructNode(func.Arguments[i]);
                                    if (i < func.Arguments.Count - 1)
                                    {
                                        _sb.Append(", ");
                                    }
                                }
                                _sb.Append(")");
                            }
                            else
                            {
                                ReconstructNode(bin.Right);
                            }
                        }
                        else
                        {
                            if (rightNeedsParens) _sb.Append("(");
                            ReconstructNode(bin.Right);
                            if (rightNeedsParens) _sb.Append(")");
                        }
                    }
                    break;

                case UnaryExpression unary:
                    if (unary.Operator.Equals("NOT", StringComparison.OrdinalIgnoreCase) &&
                        unary.Expression is BinaryExpression notBin &&
                        notBin.Operator.Equals("IN", StringComparison.OrdinalIgnoreCase))
                    {
                        ReconstructNode(notBin.Left);
                        _sb.Append(" NOT IN ");
                        if (notBin.Right is FunctionCallExpression func && func.FunctionName == "IN_LIST")
                        {
                            _sb.Append("(");
                            for (int i = 0; i < func.Arguments.Count; i++)
                            {
                                ReconstructNode(func.Arguments[i]);
                                if (i < func.Arguments.Count - 1)
                                {
                                    _sb.Append(", ");
                                }
                            }
                            _sb.Append(")");
                        }
                        else
                        {
                            ReconstructNode(notBin.Right);
                        }
                    }
                    else
                    {
                        _sb.Append(unary.Operator + " ");
                        bool needsParens = unary.Operator.Equals("NOT", StringComparison.OrdinalIgnoreCase) &&
                            unary.Expression is BinaryExpression;
                        if (needsParens) _sb.Append("(");
                        ReconstructNode(unary.Expression);
                        if (needsParens) _sb.Append(")");
                    }
                    break;

                case LiteralExpression literal:
                    if (literal.Type == LiteralType.String)
                    {
                        var prefix = literal.IsUnicode ? "N" : string.Empty;
                        _sb.Append($"{prefix}'{literal.Value.Replace("'", "''")}'");
                    }
                    else
                    {
                        _sb.Append(literal.Value);
                    }
                    break;

                case FunctionCallExpression functionCall:
                    _sb.Append(functionCall.FunctionName + "(");
                    if (functionCall.IsAllColumns)
                    {
                        _sb.Append("*");
                    }
                    else
                    {
                        for (int i = 0; i < functionCall.Arguments.Count; i++)
                        {
                            ReconstructNode(functionCall.Arguments[i]);
                            if (i < functionCall.Arguments.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }
                    }
                    _sb.Append(")");
                    break;

                case WindowFunctionExpression windowFunc:
                    _sb.Append(windowFunc.FunctionName);
                    _sb.Append("(");
                    for (int i = 0; i < windowFunc.Arguments.Count; i++)
                    {
                        ReconstructNode(windowFunc.Arguments[i]);
                        if (i < windowFunc.Arguments.Count - 1)
                        {
                            _sb.Append(", ");
                        }
                    }
                    _sb.Append(") OVER (");

                    if (windowFunc.PartitionByExpressions.Any())
                    {
                        _sb.Append("PARTITION BY ");
                        for (int i = 0; i < windowFunc.PartitionByExpressions.Count; i++)
                        {
                            ReconstructNode(windowFunc.PartitionByExpressions[i]);
                            if (i < windowFunc.PartitionByExpressions.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }

                        if (windowFunc.OrderByItems.Any() || windowFunc.Frame != null)
                        {
                            _sb.Append(" ");
                        }
                    }

                    if (windowFunc.OrderByItems.Any())
                    {
                        _sb.Append("ORDER BY ");
                        for (int i = 0; i < windowFunc.OrderByItems.Count; i++)
                        {
                            ReconstructNode(windowFunc.OrderByItems[i]);
                            if (i < windowFunc.OrderByItems.Count - 1)
                            {
                                _sb.Append(", ");
                            }
                        }

                        if (windowFunc.Frame != null)
                        {
                            _sb.Append(" ");
                        }
                    }

                    if (windowFunc.Frame != null)
                    {
                        ReconstructNode(windowFunc.Frame);
                    }

                    _sb.Append(")");
                    break;

                case WindowFrame frame:
                    switch (frame.Type)
                    {
                        case WindowFrameType.Rows:
                            _sb.Append("ROWS ");
                            break;
                        case WindowFrameType.Range:
                            _sb.Append("RANGE ");
                            break;
                        case WindowFrameType.Groups:
                            _sb.Append("GROUPS ");
                            break;
                    }

                    _sb.Append("BETWEEN ");
                    ReconstructNode(frame.StartBound);
                    _sb.Append(" AND ");
                    ReconstructNode(frame.EndBound);
                    break;

                case WindowFrameBound bound:
                    switch (bound.Type)
                    {
                        case WindowFrameBoundType.UnboundedPreceding:
                            _sb.Append("UNBOUNDED PRECEDING");
                            break;
                        case WindowFrameBoundType.UnboundedFollowing:
                            _sb.Append("UNBOUNDED FOLLOWING");
                            break;
                        case WindowFrameBoundType.CurrentRow:
                            _sb.Append("CURRENT ROW");
                            break;
                        case WindowFrameBoundType.Preceding:
                            ReconstructNode(bound.OffsetExpression);
                            _sb.Append(" PRECEDING");
                            break;
                        case WindowFrameBoundType.Following:
                            ReconstructNode(bound.OffsetExpression);
                            _sb.Append(" FOLLOWING");
                            break;
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unhandled node type: {node.GetType().Name}");
            }
        }

        private void ReconstructColumnList(IReadOnlyList<ColumnExpression> columns)
        {
            _sb.Append("(");
            for (int i = 0; i < columns.Count; i++)
            {
                ReconstructNode(columns[i]);
                if (i < columns.Count - 1)
                {
                    _sb.Append(", ");
                }
            }
            _sb.Append(")");
        }

        private void ReconstructIndexColumns(IReadOnlyList<IndexColumn> columns)
        {
            _sb.Append("(");
            for (int i = 0; i < columns.Count; i++)
            {
                ReconstructNode(columns[i].Column);
                if (columns[i].IsAscending.HasValue)
                {
                    _sb.Append(columns[i].IsAscending.Value ? " ASC" : " DESC");
                }
                if (i < columns.Count - 1)
                {
                    _sb.Append(", ");
                }
            }
            _sb.Append(")");
        }

        private void AppendReferentialActions(ForeignKeyConstraint foreignKey)
        {
            if (foreignKey.OnDelete.HasValue)
            {
                _sb.Append(" ON DELETE ");
                _sb.Append(FormatReferentialAction(foreignKey.OnDelete.Value));
            }

            if (foreignKey.OnUpdate.HasValue)
            {
                _sb.Append(" ON UPDATE ");
                _sb.Append(FormatReferentialAction(foreignKey.OnUpdate.Value));
            }
        }

        private static string FormatReferentialAction(ReferentialAction action)
        {
            return action switch
            {
                ReferentialAction.Cascade => "CASCADE",
                ReferentialAction.Restrict => "RESTRICT",
                ReferentialAction.SetNull => "SET NULL",
                ReferentialAction.SetDefault => "SET DEFAULT",
                ReferentialAction.NoAction => "NO ACTION",
                _ => action.ToString().ToUpperInvariant()
            };
        }

        private string QuoteIdentifier(string identifier, QuoteStyle quoteStyle, bool quotedFallback = false)
        {
            if (quoteStyle == QuoteStyle.None && quotedFallback)
            {
                quoteStyle = QuoteStyle.SquareBracket;
            }

            if (quoteStyle == QuoteStyle.SquareBracket)
            {
                return "[" + identifier.Replace("]", "]]") + "]";
            }

            if (quoteStyle == QuoteStyle.DoubleQuote)
            {
                return "\"" + identifier.Replace("\"", "\"\"") + "\"";
            }

            return identifier;
        }

        private static bool NeedsParenForPrecedence(string parentOp, ISqlNode child)
        {
            if (child is not BinaryExpression childBin)
            {
                return false;
            }

            string childOp = childBin.Operator.ToUpperInvariant();
            if (parentOp == "AND" && childOp == "OR") return true;
            if (parentOp == "OR" && childOp == "AND") return true;
            return false;
        }
    }
}
