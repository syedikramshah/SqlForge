using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using SqlForge.Enums;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Reconstructors
{
    public class SqlAnywhereReconstructor : BaseSqlReconstructor
    {
        private StringBuilder _sb;
        private SqlDialect _currentDialect;

        public override string Reconstruct(SqlStatement statement, SqlDialect dialect = SqlDialect.SqlAnywhere)
        {
            _sb = new StringBuilder();
            _currentDialect = dialect;

            ReconstructNode(statement);

            // Append semicolon if not already present to ensure valid SQL statement termination
            if (!_sb.ToString().TrimEnd().EndsWith(";"))
                _sb.Append(";");

            return _sb.ToString();
        }

        private void ReconstructNode(ISqlNode node)
        {
            if (node == null) return;

            switch (node)
            {
                case SqlStatement stmt:
                    ReconstructNode(stmt.Body);
                    break;

                case SelectStatement select:
                    _sb.Append("SELECT ");
                    if (select.IsDistinct) _sb.Append("DISTINCT ");

                    for (int i = 0; i < select.SelectItems.Count; i++)
                    {
                        ReconstructNode(select.SelectItems[i]);
                        if (i < select.SelectItems.Count - 1)
                            _sb.Append(", ");
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
                    _sb.Append(FormatIdentifier(createIndex.Index.Name, createIndex.Index.NameQuoted));
                    _sb.Append(" ON ");
                    ReconstructNode(createIndex.Target);
                    _sb.Append(" ");
                    ReconstructIndexColumns(createIndex.Index.Columns);
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
                    _sb.Append(FormatIdentifier(dropIndex.IndexName, dropIndex.IndexNameQuoted));
                    if (dropIndex.Target != null)
                    {
                        _sb.Append(" ON ");
                        ReconstructNode(dropIndex.Target);
                    }
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

                case SetOperatorExpression setExpr:
                    // Set operations (UNION, EXCEPT, INTERSECT) require parentheses to ensure correct
                    // parsing order. Without parentheses, chained set operations or set operations
                    // used within subqueries may be parsed with incorrect precedence. For example:
                    // SELECT A UNION SELECT B EXCEPT SELECT C needs explicit grouping to clarify
                    // whether it's (A UNION B) EXCEPT C or A UNION (B EXCEPT C).
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
                        if (selectExpr.HasExplicitAs)
                            _sb.Append(" AS ");
                        else
                            _sb.Append(" "); // Just a space for implicit aliases

                        _sb.Append(selectExpr.AliasQuoted ? $"\"{ selectExpr.Alias}\"" : selectExpr.Alias);
                    }
                    break;

                case ColumnExpression col:
                    if (!string.IsNullOrEmpty(col.SchemaName))
                    {
                        _sb.Append(col.SchemaNameQuoted ? $"\"{ col.SchemaName}\"." : $"{col.SchemaName}.");
                    }
                    if (!string.IsNullOrEmpty(col.TableAlias))
                    {
                        _sb.Append(col.TableAliasQuoted ? $"\"{ col.TableAlias}\"." : $"{col.TableAlias}.");
                    }
                    _sb.Append(col.ColumnNameQuoted ? $"\"{ col.ColumnName}\"" : col.ColumnName);
                    break;

                case FromClause from:
                    // The parser builds a single root node representing the entire FROM clause structure.
                    // For simple queries this is a TableExpression; for queries with JOINs this is a
                    // JoinExpression tree where Left contains the accumulated joins and Right contains
                    // the next table. We only need to reconstruct the first (and only) root element.
                    if (from.TableExpressions != null && from.TableExpressions.Any())
                    {
                        ReconstructNode(from.TableExpressions.First());
                    }
                    break;

                case TableExpression table:
                    if (!string.IsNullOrEmpty(table.SchemaName))
                    {
                        _sb.Append(table.SchemaNameQuoted ? $"\"{ table.SchemaName}\"." : $"{table.SchemaName}.");
                    }

                    _sb.Append(table.TableNameQuoted ? $"\"{ table.TableName}\"" : table.TableName);

                    if (!string.IsNullOrEmpty(table.Alias))
                    {
                        if (table.HasExplicitAs)
                            _sb.Append(" AS "); // Only add " AS " if explicitly present
                        else
                            _sb.Append(" "); // Just a space for implicit aliases

                        _sb.Append(table.AliasQuoted ? $"\"{ table.Alias}\"" : table.Alias);
                    }
                    break;

                case JoinExpression join:
                    {
                        // Nested joins and subqueries on either side of a JOIN need parentheses
                        // to preserve the correct association. For example:
                        // FROM (A JOIN B ON ...) JOIN C ON ... vs FROM A JOIN (B JOIN C ON ...) ON ...
                        bool leftNeedsParens = (join.Left is JoinExpression || join.Left is SubqueryExpression);
                        bool rightNeedsParens = (join.Right is JoinExpression || join.Right is SubqueryExpression);

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
                        break;
                    }

                case WhereClause where:
                    ReconstructNode(where.Condition);
                    break;

                case GroupByClause groupBy:
                    for (int i = 0; i < groupBy.GroupingExpressions.Count; i++)
                    {
                        ReconstructNode(groupBy.GroupingExpressions[i]);
                        if (i < groupBy.GroupingExpressions.Count - 1)
                            _sb.Append(", ");
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
                            _sb.Append(", ");
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
                    _sb.Append(FormatIdentifier(column.Name, column.NameQuoted));
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
                            _sb.Append(FormatIdentifier(constraint.Name, constraint.NameQuoted));
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
                            _sb.Append(FormatIdentifier(constraint.Name, constraint.NameQuoted));
                            _sb.Append(" ");
                        }
                        _sb.Append("UNIQUE ");
                        ReconstructColumnList(constraint.Columns);
                    }
                    break;

                case IndexColumn indexColumn:
                    ReconstructNode(indexColumn.Column);
                    if (indexColumn.IsAscending.HasValue)
                    {
                        _sb.Append(indexColumn.IsAscending.Value ? " ASC" : " DESC");
                    }
                    break;

                case ForeignKeyConstraint foreignKey:
                    if (!string.IsNullOrEmpty(foreignKey.Name))
                    {
                        _sb.Append("CONSTRAINT ");
                        _sb.Append(FormatIdentifier(foreignKey.Name, foreignKey.NameQuoted));
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
                        _sb.Append(FormatIdentifier(check.Name, check.NameQuoted));
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
                            _sb.Append(FormatIdentifier(action.ColumnName, action.ColumnNameQuoted));
                            break;
                        case AlterTableActionType.AlterColumn:
                            _sb.Append("ALTER COLUMN ");
                            ReconstructNode(action.Column);
                            break;
                        case AlterTableActionType.RenameTo:
                            _sb.Append("RENAME TO ");
                            _sb.Append(FormatIdentifier(action.NewTableName, action.NewTableNameQuoted));
                            break;
                        case AlterTableActionType.RenameColumn:
                            _sb.Append("RENAME COLUMN ");
                            _sb.Append(FormatIdentifier(action.ColumnName, action.ColumnNameQuoted));
                            _sb.Append(" TO ");
                            _sb.Append(FormatIdentifier(action.NewColumnName, action.NewColumnNameQuoted));
                            break;
                    }
                    break;

                case SubqueryExpression subquery:
                    // Subquery parenthesization logic:
                    // - Most subqueries need their own parentheses: (SELECT ...)
                    // - Exception: When the subquery body is a SetOperatorExpression, it already
                    //   wraps itself in parentheses (see SetOperatorExpression case above).
                    //   Adding another layer would produce ((SELECT ... UNION SELECT ...)) which,
                    //   while valid SQL, is unnecessary and harder to read.
                    bool contentIsSetOperator = (subquery.SubqueryStatement != null && subquery.SubqueryStatement.Body is SetOperatorExpression);
                    bool subqueryNeedsOwnParens = !contentIsSetOperator;

                    if (subqueryNeedsOwnParens) _sb.Append("(");
                    ReconstructNode(subquery.SubqueryStatement);
                    if (subqueryNeedsOwnParens) _sb.Append(")");

                    if (!string.IsNullOrEmpty(subquery.Alias))
                    {
                        _sb.Append(" AS ");
                        _sb.Append(subquery.AliasQuoted ? $"\"{subquery.Alias}\"" : subquery.Alias);
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
                                _sb.Append(", ");
                        }
                        _sb.Append(")");
                    }
                    break;

                case BinaryExpression bin:
                    bool wrapWithParens = false;
                    string op = bin.Operator.ToUpperInvariant();

                    // Logical operator parenthesization handles precedence between AND and OR.
                    // In SQL, AND has higher precedence than OR, so expressions like:
                    //   A OR B AND C  means  A OR (B AND C)
                    // When reconstructing, we add parentheses to preserve the parsed structure
                    // and make the intent explicit. For example, if the AST represents:
                    //   (A OR B) AND C
                    // We need parentheses because without them it would be parsed as A OR (B AND C).
                    if ((op == "AND" && ((bin.Left is BinaryExpression bl && bl.Operator.ToUpperInvariant() == "OR") || (bin.Right is BinaryExpression br && br.Operator.ToUpperInvariant() == "OR"))) ||
                        (op == "OR" && ((bin.Left is BinaryExpression bl2 && bl2.Operator.ToUpperInvariant() == "AND") || (bin.Right is BinaryExpression br2 && br2.Operator.ToUpperInvariant() == "AND"))))
                    {
                        wrapWithParens = true;
                    }

                    if (wrapWithParens) _sb.Append("(");
                    ReconstructNode(bin.Left);
                    _sb.Append($" {op} ");

                    // IN operator special handling: The right side determines parenthesis behavior.
                    // - For value lists (IN_LIST): We add parentheses around the comma-separated values
                    //   Example: col IN (1, 2, 3) -> we emit "(" + values + ")"
                    // - For subqueries: The SubqueryExpression handles its own parentheses
                    //   Example: col IN (SELECT ...) -> SubqueryExpression emits "(SELECT ...)"
                    if (op == "IN")
                    {
                        if (bin.Right is FunctionCallExpression func && func.FunctionName == "IN_LIST")
                        {
                            _sb.Append("(");
                            for (int i = 0; i < func.Arguments.Count; i++)
                            {
                                ReconstructNode(func.Arguments[i]);
                                if (i < func.Arguments.Count - 1)
                                    _sb.Append(", ");
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
                        ReconstructNode(bin.Right);
                    }
                    if (wrapWithParens) _sb.Append(")");
                    break;

                case UnaryExpression unary:
                    _sb.Append(unary.Operator + " ");

                    // Unary operator parenthesization:
                    // - EXISTS: SubqueryExpression handles its own parentheses, no extra needed
                    // - NOT: Add parentheses when applied to logical/comparison expressions to
                    //   preserve meaning. For example:
                    //   NOT (A AND B) - without parens would be parsed as (NOT A) AND B
                    //   NOT (col IS NULL) - without parens would try to apply NOT to just 'col'
                    //   NOT (col IN (1,2)) - maintains correct grouping
                    bool unaryNeedsOuterParens = false;

                    if (unary.Operator.Equals("NOT", StringComparison.OrdinalIgnoreCase) && unary.Expression is BinaryExpression bexp &&
                        (bexp.Operator.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                         bexp.Operator.Equals("OR", StringComparison.OrdinalIgnoreCase) ||
                         bexp.Operator.Equals("IS", StringComparison.OrdinalIgnoreCase) ||
                         bexp.Operator.Equals("IS NOT", StringComparison.OrdinalIgnoreCase) ||
                         bexp.Operator.Equals("LIKE", StringComparison.OrdinalIgnoreCase) ||
                         bexp.Operator.Equals("IN", StringComparison.OrdinalIgnoreCase)))
                    {
                        unaryNeedsOuterParens = true;
                    }

                    if (unaryNeedsOuterParens) _sb.Append("(");
                    ReconstructNode(unary.Expression);
                    if (unaryNeedsOuterParens) _sb.Append(")");
                    break;

                case LiteralExpression literal:
                    if (literal.Type == LiteralType.String)
                        _sb.Append($"'{literal.Value.Replace("'", "''")}'");
                    else
                        _sb.Append(literal.Value);
                    break;

                case FunctionCallExpression funtionCallExp:
                    _sb.Append(funtionCallExp.FunctionName + "(");
                    if (funtionCallExp.IsAllColumns)
                        _sb.Append("*");
                    else
                    {
                        for (int i = 0; i < funtionCallExp.Arguments.Count; i++)
                        {
                            ReconstructNode(funtionCallExp.Arguments[i]);
                            if (i < funtionCallExp.Arguments.Count - 1)
                                _sb.Append(", ");
                        }
                    }
                    _sb.Append(")");
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
                ReconstructNode(columns[i]);
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

        private string FormatIdentifier(string value, bool quoted)
        {
            return quoted ? $"\"{value}\"" : value;
        }
    }
}