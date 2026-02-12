using System;
using System.Collections.Generic;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{
    /// <summary>
    /// Factory responsible for providing or orchestrating the parsing of top-level SQL statements
    /// for the MySQL dialect.
    /// </summary>
    public class MySqlStatementParserFactory : IStatementParserFactory
    {
        private readonly MySqlSelectStatementParser _selectStatementParser;
        private readonly IExpressionParser _expressionParser;

        public MySqlStatementParserFactory(MySqlSelectStatementParser selectStatementParser, IExpressionParser expressionParser)
        {
            _selectStatementParser = selectStatementParser;
            _expressionParser = expressionParser;
        }

        public SqlStatement ParseStatement(IParserContext context)
        {
            ISqlNode statementBody = ParseSingleStatementBody(context);

            while (context.IsKeyword("EXCEPT") || context.IsKeyword("UNION") || context.IsKeyword("INTERSECT"))
            {
                Token operatorToken = context.ConsumeToken();
                SetOperatorType opType;

                string upper = operatorToken.Value.ToUpperInvariant();
                if (upper == "EXCEPT")
                    opType = SetOperatorType.Except;
                else if (upper == "UNION")
                    opType = SetOperatorType.Union;
                else if (upper == "INTERSECT")
                    opType = SetOperatorType.Intersect;
                else
                    throw new SqlParseException("Unsupported set operator: " + operatorToken.Value, operatorToken.StartIndex);

                if (context.IsKeyword("ALL"))
                {
                    if (opType == SetOperatorType.Union)
                    {
                        context.ConsumeToken("ALL");
                        opType = SetOperatorType.UnionAll;
                    }
                    else
                    {
                        throw new SqlParseException($"{upper} ALL is not supported in MySQL.", context.PeekToken().StartIndex);
                    }
                }

                ISqlNode right = ParseSingleStatementBody(context);

                var setOpExpr = new SetOperatorExpression
                {
                    Operator = opType,
                    Left = statementBody,
                    Right = right
                };

                statementBody = setOpExpr;
            }

            if (statementBody is SelectStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.Select,
                    Body = statementBody
                };
            }

            if (statementBody is InsertStatement insert)
            {
                return new SqlStatement
                {
                    Type = insert.IsReplace ? StatementType.Replace : StatementType.Insert,
                    Body = statementBody
                };
            }

            if (statementBody is UpdateStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.Update,
                    Body = statementBody
                };
            }

            if (statementBody is DeleteStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.Delete,
                    Body = statementBody
                };
            }

            if (statementBody is CreateTableStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.Create,
                    Body = statementBody
                };
            }

            if (statementBody is CreateIndexStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.CreateIndex,
                    Body = statementBody
                };
            }

            if (statementBody is DropTableStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.Drop,
                    Body = statementBody
                };
            }

            if (statementBody is DropIndexStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.DropIndex,
                    Body = statementBody
                };
            }

            if (statementBody is AlterTableStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.Alter,
                    Body = statementBody
                };
            }

            return new SqlStatement
            {
                Type = StatementType.Unknown,
                Body = statementBody
            };
        }

        private ISqlNode ParseSingleStatementBody(IParserContext context)
        {
            if (_selectStatementParser.CanParse(context))
            {
                return _selectStatementParser.Parse(context).Body;
            }

            if (context.IsKeyword("INSERT") || context.IsKeyword("REPLACE"))
            {
                return ParseInsertStatement(context);
            }

            if (context.IsKeyword("UPDATE"))
            {
                return ParseUpdateStatement(context);
            }

            if (context.IsKeyword("DELETE"))
            {
                return ParseDeleteStatement(context);
            }

            if (context.IsKeyword("CREATE"))
            {
                if (IsCreateIndexStatement(context))
                {
                    return ParseCreateIndexStatement(context);
                }

                return ParseCreateTableStatement(context);
            }

            if (context.IsKeyword("DROP"))
            {
                if (IsDropIndexStatement(context))
                {
                    return ParseDropIndexStatement(context);
                }

                return ParseDropTableStatement(context);
            }

            if (context.IsKeyword("ALTER"))
            {
                return ParseAlterTableStatement(context);
            }

            if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
            {
                context.ConsumeToken("(", TokenType.Parenthesis);
                var body = ParseStatement(context).Body;
                context.ConsumeToken(")", TokenType.Parenthesis);
                return body;
            }

            throw new SqlParseException($"Unsupported statement type: '{context.CurrentToken().Value}'", context.CurrentToken().StartIndex);
        }

        public IStatementParser GetParserForStatementType(IParserContext context)
        {
            if (_selectStatementParser.CanParse(context))
            {
                return _selectStatementParser;
            }
            return null;
        }

        private InsertStatement ParseInsertStatement(IParserContext context)
        {
            bool isReplace = false;
            if (context.IsKeyword("REPLACE"))
            {
                context.ConsumeToken("REPLACE");
                isReplace = true;
            }
            else
            {
                context.ConsumeToken("INSERT");
            }

            var insert = new InsertStatement { IsReplace = isReplace };

            if (context.IsKeyword("LOW_PRIORITY"))
            {
                context.ConsumeToken("LOW_PRIORITY");
            }

            if (context.IsKeyword("IGNORE"))
            {
                context.ConsumeToken("IGNORE");
                insert.IsIgnore = true;
            }

            if (context.IsKeyword("INTO"))
            {
                context.ConsumeToken("INTO");
            }

            insert.Target = ParseTableName(context);

            if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
            {
                context.ConsumeToken("(", TokenType.Parenthesis);
                do
                {
                    var colToken = context.ConsumeToken();
                    if (colToken.Type != TokenType.Identifier && colToken.Type != TokenType.Keyword)
                    {
                        throw new SqlParseException("Expected column name in INSERT column list.", colToken.StartIndex);
                    }

                    insert.Columns.Add(new ColumnExpression
                    {
                        ColumnName = colToken.Value,
                        ColumnNameQuoted = colToken.IsQuoted,
                        ColumnQuoteStyle = colToken.QuoteStyle
                    });
                } while (context.MatchToken(",", TokenType.Comma));

                context.ConsumeToken(")", TokenType.Parenthesis);
            }

            if (context.IsKeyword("SET"))
            {
                context.ConsumeToken("SET");
                insert.SetAssignments = ParseAssignments(context);
            }
            else if (context.IsKeyword("VALUES") || IsValueToken(context.PeekToken()))
            {
                context.ConsumeToken();
                do
                {
                    context.ConsumeToken("(", TokenType.Parenthesis);
                    var row = new List<ISqlNode>();
                    if (!(context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == ")"))
                    {
                        do
                        {
                            row.Add(_expressionParser.Parse(context));
                        } while (context.MatchToken(",", TokenType.Comma));
                    }
                    context.ConsumeToken(")", TokenType.Parenthesis);
                    insert.Values.Add(row);
                } while (context.MatchToken(",", TokenType.Comma));
            }
            else if (context.IsKeyword("SELECT"))
            {
                insert.SelectStatement = ParseStatement(context);
            }
            else
            {
                throw new SqlParseException("Expected VALUES, SET, or SELECT in INSERT statement.", context.PeekToken().StartIndex);
            }

            if (context.IsKeyword("ON") && context.PeekToken(1).Type == TokenType.Keyword &&
                context.PeekToken(1).Value.Equals("DUPLICATE", StringComparison.OrdinalIgnoreCase))
            {
                context.ConsumeToken("ON");
                context.ConsumeToken("DUPLICATE");
                context.ConsumeToken("KEY");
                context.ConsumeToken("UPDATE");
                insert.OnDuplicateKeyUpdate = ParseAssignments(context);
            }

            return insert;
        }

        private UpdateStatement ParseUpdateStatement(IParserContext context)
        {
            context.ConsumeToken("UPDATE");
            var update = new UpdateStatement();

            if (context.IsKeyword("LOW_PRIORITY"))
            {
                context.ConsumeToken("LOW_PRIORITY");
                update.IsLowPriority = true;
            }

            if (context.IsKeyword("IGNORE"))
            {
                context.ConsumeToken("IGNORE");
                update.IsIgnore = true;
            }

            update.Target = ParseTableName(context);

            context.ConsumeToken("SET");
            update.SetClauses = ParseAssignments(context);

            if (context.IsKeyword("WHERE"))
            {
                context.ConsumeToken("WHERE");
                update.WhereClause = new WhereClause { Condition = _expressionParser.Parse(context) };
            }

            if (context.IsKeyword("ORDER"))
            {
                context.ConsumeToken("ORDER");
                context.ConsumeToken("BY");
                update.OrderByClause = ParseOrderByClause(context);
            }

            if (context.IsKeyword("LIMIT"))
            {
                update.LimitClause = ParseLimitClause(context);
            }

            return update;
        }

        private DeleteStatement ParseDeleteStatement(IParserContext context)
        {
            context.ConsumeToken("DELETE");
            var delete = new DeleteStatement();

            if (context.IsKeyword("LOW_PRIORITY"))
            {
                context.ConsumeToken("LOW_PRIORITY");
                delete.IsLowPriority = true;
            }

            if (context.IsKeyword("QUICK"))
            {
                context.ConsumeToken("QUICK");
                delete.IsQuick = true;
            }

            if (context.IsKeyword("IGNORE"))
            {
                context.ConsumeToken("IGNORE");
                delete.IsIgnore = true;
            }

            context.ConsumeToken("FROM");
            delete.Target = ParseTableName(context);

            if (context.IsKeyword("WHERE"))
            {
                context.ConsumeToken("WHERE");
                delete.WhereClause = new WhereClause { Condition = _expressionParser.Parse(context) };
            }

            if (context.IsKeyword("ORDER"))
            {
                context.ConsumeToken("ORDER");
                context.ConsumeToken("BY");
                delete.OrderByClause = ParseOrderByClause(context);
            }

            if (context.IsKeyword("LIMIT"))
            {
                delete.LimitClause = ParseLimitClause(context);
            }

            return delete;
        }

        private CreateTableStatement ParseCreateTableStatement(IParserContext context)
        {
            context.ConsumeToken("CREATE");
            var create = new CreateTableStatement();

            if (context.IsKeyword("TEMPORARY"))
            {
                context.ConsumeToken("TEMPORARY");
                create.IsTemporary = true;
            }

            context.ConsumeToken("TABLE");

            if (context.IsKeyword("IF") && context.PeekToken(1).Type == TokenType.Keyword &&
                context.PeekToken(1).Value.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            {
                context.ConsumeToken("IF");
                context.ConsumeToken("NOT");
                context.ConsumeToken("EXISTS");
                create.IfNotExists = true;
            }

            create.Target = ParseTableName(context);

            if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
            {
                context.ConsumeToken("(", TokenType.Parenthesis);
                while (!(context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == ")"))
                {
                    if (IsTableConstraintStart(context))
                    {
                        create.Constraints.Add(ParseTableConstraint(context));
                    }
                    else if (IsIndexStart(context))
                    {
                        create.Indexes.Add(ParseIndexDefinition(context));
                    }
                    else
                    {
                        create.Columns.Add(ParseColumnDefinition(context));
                    }

                    if (context.MatchToken(",", TokenType.Comma))
                    {
                        continue;
                    }
                }
                context.ConsumeToken(")", TokenType.Parenthesis);
            }

            create.TableOptions.AddRange(ParseTableOptions(context));

            return create;
        }

        private DropTableStatement ParseDropTableStatement(IParserContext context)
        {
            context.ConsumeToken("DROP");
            context.ConsumeToken("TABLE");
            var drop = new DropTableStatement();

            if (context.IsKeyword("IF"))
            {
                context.ConsumeToken("IF");
                context.ConsumeToken("EXISTS");
                drop.IfExists = true;
            }

            do
            {
                drop.Targets.Add(ParseTableName(context));
            } while (context.MatchToken(",", TokenType.Comma));

            return drop;
        }

        private AlterTableStatement ParseAlterTableStatement(IParserContext context)
        {
            context.ConsumeToken("ALTER");
            context.ConsumeToken("TABLE");
            var alter = new AlterTableStatement
            {
                Target = ParseTableName(context)
            };

            do
            {
                alter.Actions.Add(ParseAlterAction(context));
            } while (context.MatchToken(",", TokenType.Comma));

            return alter;
        }

        private AlterTableAction ParseAlterAction(IParserContext context)
        {
            if (context.IsKeyword("ADD"))
            {
                context.ConsumeToken("ADD");
                if (context.IsKeyword("COLUMN"))
                {
                    context.ConsumeToken("COLUMN");
                }

                if (IsAddConstraintStart(context))
                {
                    return new AlterTableAction
                    {
                        ActionType = AlterTableActionType.AddConstraint,
                        Constraint = ParseTableConstraint(context)
                    };
                }

                if (IsIndexStart(context))
                {
                    return new AlterTableAction
                    {
                        ActionType = AlterTableActionType.AddIndex,
                        Index = ParseIndexDefinition(context)
                    };
                }

                return new AlterTableAction
                {
                    ActionType = AlterTableActionType.AddColumn,
                    Column = ParseColumnDefinition(context)
                };
            }

            if (context.IsKeyword("DROP"))
            {
                context.ConsumeToken("DROP");
                if (context.IsKeyword("PRIMARY"))
                {
                    context.ConsumeToken("PRIMARY");
                    context.ConsumeToken("KEY");
                    return new AlterTableAction
                    {
                        ActionType = AlterTableActionType.DropIndex,
                        DropPrimaryKey = true
                    };
                }

                if (context.IsKeyword("INDEX") || context.IsKeyword("KEY"))
                {
                    context.ConsumeToken();
                    var indexNameToken = context.ConsumeToken();
                    if (indexNameToken.Type != TokenType.Identifier && indexNameToken.Type != TokenType.Keyword)
                    {
                        throw new SqlParseException("Expected index name after DROP INDEX.", indexNameToken.StartIndex);
                    }

                    return new AlterTableAction
                    {
                        ActionType = AlterTableActionType.DropIndex,
                        IndexName = indexNameToken.Value
                    };
                }

                if (context.IsKeyword("COLUMN"))
                {
                    context.ConsumeToken("COLUMN");
                }

                var nameToken = context.ConsumeToken();
                if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected column name after DROP COLUMN.", nameToken.StartIndex);
                }

                return new AlterTableAction
                {
                    ActionType = AlterTableActionType.DropColumn,
                    ColumnName = nameToken.Value,
                    ColumnNameQuoted = nameToken.IsQuoted,
                    ColumnNameQuoteStyle = nameToken.QuoteStyle
                };
            }

            if (context.IsKeyword("MODIFY"))
            {
                context.ConsumeToken("MODIFY");
                if (context.IsKeyword("COLUMN"))
                {
                    context.ConsumeToken("COLUMN");
                }

                return new AlterTableAction
                {
                    ActionType = AlterTableActionType.ModifyColumn,
                    Column = ParseColumnDefinition(context)
                };
            }

            if (context.IsKeyword("CHANGE"))
            {
                context.ConsumeToken("CHANGE");
                if (context.IsKeyword("COLUMN"))
                {
                    context.ConsumeToken("COLUMN");
                }

                var oldToken = context.ConsumeToken();
                if (oldToken.Type != TokenType.Identifier && oldToken.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected old column name in CHANGE COLUMN.", oldToken.StartIndex);
                }

                var newColumn = ParseColumnDefinition(context);
                return new AlterTableAction
                {
                    ActionType = AlterTableActionType.ChangeColumn,
                    ColumnName = oldToken.Value,
                    ColumnNameQuoted = oldToken.IsQuoted,
                    ColumnNameQuoteStyle = oldToken.QuoteStyle,
                    Column = newColumn
                };
            }

            if (context.IsKeyword("RENAME"))
            {
                context.ConsumeToken("RENAME");
                if (context.IsKeyword("TO"))
                {
                    context.ConsumeToken("TO");
                    var nameToken = context.ConsumeToken();
                    if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
                    {
                        throw new SqlParseException("Expected table name after RENAME TO.", nameToken.StartIndex);
                    }

                    return new AlterTableAction
                    {
                        ActionType = AlterTableActionType.RenameTo,
                        NewTableName = nameToken.Value,
                        NewTableNameQuoted = nameToken.IsQuoted,
                        NewTableNameQuoteStyle = nameToken.QuoteStyle
                    };
                }

                if (context.IsKeyword("COLUMN"))
                {
                    context.ConsumeToken("COLUMN");
                    var oldToken = context.ConsumeToken();
                    context.ConsumeToken("TO");
                    var newToken = context.ConsumeToken();
                    if (oldToken.Type != TokenType.Identifier && oldToken.Type != TokenType.Keyword)
                    {
                        throw new SqlParseException("Expected column name after RENAME COLUMN.", oldToken.StartIndex);
                    }
                    if (newToken.Type != TokenType.Identifier && newToken.Type != TokenType.Keyword)
                    {
                        throw new SqlParseException("Expected new column name after RENAME COLUMN.", newToken.StartIndex);
                    }

                    return new AlterTableAction
                    {
                        ActionType = AlterTableActionType.RenameColumn,
                        ColumnName = oldToken.Value,
                        ColumnNameQuoted = oldToken.IsQuoted,
                        ColumnNameQuoteStyle = oldToken.QuoteStyle,
                        NewColumnName = newToken.Value,
                        NewColumnNameQuoted = newToken.IsQuoted,
                        NewColumnNameQuoteStyle = newToken.QuoteStyle
                    };
                }
            }

            throw new SqlParseException("Unsupported ALTER TABLE action.", context.PeekToken().StartIndex);
        }

        private List<AssignmentExpression> ParseAssignments(IParserContext context)
        {
            var assignments = new List<AssignmentExpression>();
            do
            {
                var column = ParseColumnReference(context);
                context.ConsumeToken("=", TokenType.Operator);
                var value = _expressionParser.Parse(context);
                assignments.Add(new AssignmentExpression { Column = column, Value = value });
            } while (context.MatchToken(",", TokenType.Comma));

            return assignments;
        }

        private ColumnExpression ParseColumnReference(IParserContext context)
        {
            var token = context.ConsumeToken();
            if (token.Type != TokenType.Identifier && token.Type != TokenType.Keyword)
            {
                throw new SqlParseException("Expected column name.", token.StartIndex);
            }

            var column = new ColumnExpression
            {
                ColumnName = token.Value,
                ColumnNameQuoted = token.IsQuoted,
                ColumnQuoteStyle = token.QuoteStyle
            };

            if (context.PeekToken().Value == ".")
            {
                context.ConsumeToken(".", TokenType.Operator);
                var second = context.ConsumeToken();
                if (second.Type != TokenType.Identifier && second.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected column name after '.'.", second.StartIndex);
                }

                column.TableAlias = token.Value;
                column.TableAliasQuoted = token.IsQuoted;
                column.TableAliasQuoteStyle = token.QuoteStyle;
                column.ColumnName = second.Value;
                column.ColumnNameQuoted = second.IsQuoted;
                column.ColumnQuoteStyle = second.QuoteStyle;
            }

            return column;
        }

        private TableExpression ParseTableName(IParserContext context)
        {
            var id1 = context.ConsumeToken();
            if (id1.Type != TokenType.Identifier && id1.Type != TokenType.Keyword)
            {
                throw new SqlParseException("Expected table name.", id1.StartIndex);
            }

            var table = new TableExpression
            {
                TableName = id1.Value,
                TableNameQuoted = id1.IsQuoted,
                TableQuoteStyle = id1.QuoteStyle
            };

            if (context.PeekToken().Value == ".")
            {
                context.ConsumeToken(".", TokenType.Operator);
                var id2 = context.ConsumeToken();
                if (id2.Type != TokenType.Identifier && id2.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected table name after schema prefix.", id2.StartIndex);
                }

                table.SchemaName = id1.Value;
                table.SchemaNameQuoted = id1.IsQuoted;
                table.SchemaQuoteStyle = id1.QuoteStyle;
                table.TableName = id2.Value;
                table.TableNameQuoted = id2.IsQuoted;
                table.TableQuoteStyle = id2.QuoteStyle;
            }

            return table;
        }

        private ColumnDefinition ParseColumnDefinition(IParserContext context)
        {
            var nameToken = context.ConsumeToken();
            if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
            {
                throw new SqlParseException("Expected column name in definition.", nameToken.StartIndex);
            }

            var column = new ColumnDefinition
            {
                Name = nameToken.Value,
                NameQuoted = nameToken.IsQuoted,
                NameQuoteStyle = nameToken.QuoteStyle
            };

            var typeToken = context.ConsumeToken();
            if (typeToken.Type != TokenType.Identifier && typeToken.Type != TokenType.Keyword)
            {
                throw new SqlParseException("Expected data type in column definition.", typeToken.StartIndex);
            }

            string dataType = typeToken.Value;
            if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
            {
                dataType += ParseParenthesizedTokens(context);
            }

            while (context.IsKeyword("UNSIGNED") || context.IsKeyword("ZEROFILL") || context.IsKeyword("BINARY"))
            {
                var modifier = context.ConsumeToken();
                dataType += " " + modifier.Value.ToUpperInvariant();
            }

            column.DataType = dataType;

            while (context.PeekToken().Type == TokenType.Keyword)
            {
                if (context.IsKeyword("NOT"))
                {
                    context.ConsumeToken("NOT");
                    context.ConsumeToken("NULL");
                    column.IsNullable = false;
                    continue;
                }

                if (context.IsKeyword("NULL"))
                {
                    context.ConsumeToken("NULL");
                    column.IsNullable = true;
                    continue;
                }

                if (context.IsKeyword("DEFAULT"))
                {
                    context.ConsumeToken("DEFAULT");
                    column.DefaultExpression = _expressionParser.Parse(context);
                    continue;
                }

                if (context.IsKeyword("PRIMARY"))
                {
                    context.ConsumeToken("PRIMARY");
                    context.ConsumeToken("KEY");
                    column.IsPrimaryKey = true;
                    continue;
                }

                if (context.IsKeyword("UNIQUE"))
                {
                    context.ConsumeToken("UNIQUE");
                    if (context.IsKeyword("KEY") || context.IsKeyword("INDEX"))
                    {
                        context.ConsumeToken();
                    }
                    column.IsUnique = true;
                    continue;
                }

                if (context.IsKeyword("CHARACTER"))
                {
                    context.ConsumeToken("CHARACTER");
                    context.ConsumeToken("SET");
                    var charsetToken = context.ConsumeToken();
                    column.CharacterSet = charsetToken.Value;
                    continue;
                }

                if (context.IsKeyword("CHARSET"))
                {
                    context.ConsumeToken("CHARSET");
                    var charsetToken = context.ConsumeToken();
                    column.CharacterSet = charsetToken.Value;
                    continue;
                }

                if (context.IsKeyword("COLLATE"))
                {
                    context.ConsumeToken("COLLATE");
                    var collateToken = context.ConsumeToken();
                    column.Collation = collateToken.Value;
                    continue;
                }

                if (context.IsKeyword("COMMENT"))
                {
                    context.ConsumeToken("COMMENT");
                    var commentToken = context.ConsumeToken();
                    if (commentToken.Type != TokenType.StringLiteral)
                    {
                        throw new SqlParseException("Expected string literal after COMMENT.", commentToken.StartIndex);
                    }
                    column.Comment = commentToken.Value;
                    continue;
                }

                if (context.IsKeyword("AUTO_INCREMENT"))
                {
                    context.ConsumeToken("AUTO_INCREMENT");
                    column.AutoIncrement = true;
                    continue;
                }

                break;
            }

            return column;
        }

        private ITableConstraint ParseTableConstraint(IParserContext context)
        {
            string name = null;
            bool nameQuoted = false;
            QuoteStyle nameQuoteStyle = QuoteStyle.None;

            if (context.IsKeyword("CONSTRAINT"))
            {
                context.ConsumeToken("CONSTRAINT");
                var nameToken = context.ConsumeToken();
                if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected constraint name.", nameToken.StartIndex);
                }

                name = nameToken.Value;
                nameQuoted = nameToken.IsQuoted;
                nameQuoteStyle = nameToken.QuoteStyle;
            }

            if (context.IsKeyword("PRIMARY"))
            {
                context.ConsumeToken("PRIMARY");
                context.ConsumeToken("KEY");
                return new TableConstraint
                {
                    Type = ConstraintType.PrimaryKey,
                    Name = name,
                    NameQuoted = nameQuoted,
                    NameQuoteStyle = nameQuoteStyle,
                    Columns = ParseConstraintColumns(context)
                };
            }

            if (context.IsKeyword("UNIQUE"))
            {
                context.ConsumeToken("UNIQUE");
                if (context.IsKeyword("KEY") || context.IsKeyword("INDEX"))
                {
                    context.ConsumeToken();
                }

                if (name == null && context.PeekToken().Type == TokenType.Identifier &&
                    !(context.PeekToken(1).Type == TokenType.Parenthesis && context.PeekToken(1).Value == "("))
                {
                    var nameToken = context.ConsumeToken();
                    name = nameToken.Value;
                    nameQuoted = nameToken.IsQuoted;
                    nameQuoteStyle = nameToken.QuoteStyle;
                }

                return new TableConstraint
                {
                    Type = ConstraintType.UniqueKey,
                    Name = name,
                    NameQuoted = nameQuoted,
                    NameQuoteStyle = nameQuoteStyle,
                    Columns = ParseConstraintColumns(context)
                };
            }

            if (context.IsKeyword("FOREIGN"))
            {
                context.ConsumeToken("FOREIGN");
                context.ConsumeToken("KEY");
                var columns = ParseConstraintColumns(context);
                context.ConsumeToken("REFERENCES");
                var referencedTable = ParseTableName(context);
                var referencedColumns = ParseConstraintColumns(context);

                var fk = new ForeignKeyConstraint
                {
                    Name = name,
                    NameQuoted = nameQuoted,
                    NameQuoteStyle = nameQuoteStyle,
                    Columns = columns,
                    ReferencedTable = referencedTable,
                    ReferencedColumns = referencedColumns
                };

                while (context.IsKeyword("ON"))
                {
                    context.ConsumeToken("ON");
                    if (context.IsKeyword("DELETE"))
                    {
                        context.ConsumeToken("DELETE");
                        fk.OnDelete = ParseReferentialAction(context);
                    }
                    else if (context.IsKeyword("UPDATE"))
                    {
                        context.ConsumeToken("UPDATE");
                        fk.OnUpdate = ParseReferentialAction(context);
                    }
                    else
                    {
                        throw new SqlParseException("Expected DELETE or UPDATE after ON in foreign key constraint.", context.PeekToken().StartIndex);
                    }
                }

                return fk;
            }

            if (context.IsKeyword("CHECK"))
            {
                context.ConsumeToken("CHECK");
                context.ConsumeToken("(", TokenType.Parenthesis);
                var condition = _expressionParser.Parse(context);
                context.ConsumeToken(")", TokenType.Parenthesis);

                return new CheckConstraint
                {
                    Name = name,
                    NameQuoted = nameQuoted,
                    NameQuoteStyle = nameQuoteStyle,
                    Condition = condition
                };
            }

            throw new SqlParseException("Unsupported table constraint.", context.PeekToken().StartIndex);
        }

        private IndexDefinition ParseIndexDefinition(IParserContext context)
        {
            string constraintName = null;
            bool constraintNameQuoted = false;
            QuoteStyle constraintNameQuoteStyle = QuoteStyle.None;
            if (context.IsKeyword("CONSTRAINT"))
            {
                context.ConsumeToken("CONSTRAINT");
                var nameToken = context.ConsumeToken();
                if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected constraint name.", nameToken.StartIndex);
                }
                constraintName = nameToken.Value;
                constraintNameQuoted = nameToken.IsQuoted;
                constraintNameQuoteStyle = nameToken.QuoteStyle;
            }

            var index = new IndexDefinition
            {
                Name = constraintName,
                NameQuoted = constraintNameQuoted,
                NameQuoteStyle = constraintNameQuoteStyle
            };

            if (context.IsKeyword("PRIMARY"))
            {
                context.ConsumeToken("PRIMARY");
                context.ConsumeToken("KEY");
                index.Type = IndexType.Primary;
            }
            else if (context.IsKeyword("UNIQUE"))
            {
                context.ConsumeToken("UNIQUE");
                if (context.IsKeyword("KEY") || context.IsKeyword("INDEX"))
                {
                    context.ConsumeToken();
                }
                index.Type = IndexType.Unique;
            }
            else if (context.IsKeyword("FULLTEXT"))
            {
                context.ConsumeToken("FULLTEXT");
                if (context.IsKeyword("KEY") || context.IsKeyword("INDEX"))
                {
                    context.ConsumeToken();
                }
                index.Type = IndexType.Fulltext;
            }
            else if (context.IsKeyword("SPATIAL"))
            {
                context.ConsumeToken("SPATIAL");
                if (context.IsKeyword("KEY") || context.IsKeyword("INDEX"))
                {
                    context.ConsumeToken();
                }
                index.Type = IndexType.Spatial;
            }
            else if (context.IsKeyword("INDEX") || context.IsKeyword("KEY"))
            {
                context.ConsumeToken();
                index.Type = IndexType.Index;
            }
            else
            {
                throw new SqlParseException("Expected index definition.", context.PeekToken().StartIndex);
            }

            if (context.PeekToken().Type != TokenType.Parenthesis &&
                (context.PeekToken().Type == TokenType.Identifier || context.PeekToken().Type == TokenType.Keyword))
            {
                if (index.Name == null)
                {
                    var nameToken = context.ConsumeToken();
                    index.Name = nameToken.Value;
                    index.NameQuoted = nameToken.IsQuoted;
                    index.NameQuoteStyle = nameToken.QuoteStyle;
                }
            }

            if (context.IsKeyword("USING"))
            {
                context.ConsumeToken("USING");
                index.UsingType = context.ConsumeToken().Value.ToUpperInvariant();
            }

            index.Columns = ParseIndexColumns(context);

            if (context.IsKeyword("USING"))
            {
                context.ConsumeToken("USING");
                index.UsingType = context.ConsumeToken().Value.ToUpperInvariant();
            }

            return index;
        }

        private List<IndexColumn> ParseIndexColumns(IParserContext context)
        {
            context.ConsumeToken("(", TokenType.Parenthesis);
            var columns = new List<IndexColumn>();
            do
            {
                var col = ParseColumnReference(context);
                var indexColumn = new IndexColumn { Column = col };

                if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
                {
                    context.ConsumeToken("(", TokenType.Parenthesis);
                    var lengthToken = context.ConsumeToken();
                    if (lengthToken.Type != TokenType.NumericLiteral)
                    {
                        throw new SqlParseException("Expected index column length.", lengthToken.StartIndex);
                    }
                    indexColumn.LengthExpression = new LiteralExpression
                    {
                        Value = lengthToken.Value,
                        Type = LiteralType.Number
                    };
                    context.ConsumeToken(")", TokenType.Parenthesis);
                }

                if (context.IsKeyword("ASC"))
                {
                    context.ConsumeToken("ASC");
                    indexColumn.IsAscending = true;
                }
                else if (context.IsKeyword("DESC"))
                {
                    context.ConsumeToken("DESC");
                    indexColumn.IsAscending = false;
                }

                columns.Add(indexColumn);
            } while (context.MatchToken(",", TokenType.Comma));
            context.ConsumeToken(")", TokenType.Parenthesis);
            return columns;
        }

        private CreateIndexStatement ParseCreateIndexStatement(IParserContext context)
        {
            context.ConsumeToken("CREATE");
            var index = new IndexDefinition { Type = IndexType.Index };

            if (context.IsKeyword("UNIQUE"))
            {
                context.ConsumeToken("UNIQUE");
                index.Type = IndexType.Unique;
            }
            else if (context.IsKeyword("FULLTEXT"))
            {
                context.ConsumeToken("FULLTEXT");
                index.Type = IndexType.Fulltext;
            }
            else if (context.IsKeyword("SPATIAL"))
            {
                context.ConsumeToken("SPATIAL");
                index.Type = IndexType.Spatial;
            }

            if (context.IsKeyword("INDEX") || context.IsKeyword("KEY"))
            {
                context.ConsumeToken();
            }
            else
            {
                throw new SqlParseException("Expected INDEX or KEY after CREATE.", context.PeekToken().StartIndex);
            }

            var nameToken = context.ConsumeToken();
            if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
            {
                throw new SqlParseException("Expected index name.", nameToken.StartIndex);
            }

            index.Name = nameToken.Value;
            index.NameQuoted = nameToken.IsQuoted;
            index.NameQuoteStyle = nameToken.QuoteStyle;

            context.ConsumeToken("ON");
            var target = ParseTableName(context);

            if (context.IsKeyword("USING"))
            {
                context.ConsumeToken("USING");
                index.UsingType = context.ConsumeToken().Value.ToUpperInvariant();
            }

            index.Columns = ParseIndexColumns(context);

            if (context.IsKeyword("USING"))
            {
                context.ConsumeToken("USING");
                index.UsingType = context.ConsumeToken().Value.ToUpperInvariant();
            }

            return new CreateIndexStatement
            {
                Index = index,
                Target = target
            };
        }

        private DropIndexStatement ParseDropIndexStatement(IParserContext context)
        {
            context.ConsumeToken("DROP");
            context.ConsumeToken("INDEX");
            var drop = new DropIndexStatement();

            var nameToken = context.ConsumeToken();
            if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
            {
                throw new SqlParseException("Expected index name.", nameToken.StartIndex);
            }

            drop.IndexName = nameToken.Value;
            drop.IndexNameQuoted = nameToken.IsQuoted;
            drop.IndexNameQuoteStyle = nameToken.QuoteStyle;

            context.ConsumeToken("ON");
            drop.Target = ParseTableName(context);

            return drop;
        }

        private ReferentialAction ParseReferentialAction(IParserContext context)
        {
            if (context.IsKeyword("CASCADE"))
            {
                context.ConsumeToken("CASCADE");
                return ReferentialAction.Cascade;
            }

            if (context.IsKeyword("RESTRICT"))
            {
                context.ConsumeToken("RESTRICT");
                return ReferentialAction.Restrict;
            }

            if (context.IsKeyword("SET"))
            {
                context.ConsumeToken("SET");
                if (context.IsKeyword("NULL"))
                {
                    context.ConsumeToken("NULL");
                    return ReferentialAction.SetNull;
                }

                context.ConsumeToken("DEFAULT");
                return ReferentialAction.SetDefault;
            }

            if (context.IsKeyword("NO"))
            {
                context.ConsumeToken("NO");
                context.ConsumeToken("ACTION");
                return ReferentialAction.NoAction;
            }

            throw new SqlParseException("Unsupported referential action.", context.PeekToken().StartIndex);
        }

        private static bool IsTableConstraintStart(IParserContext context)
        {
            return context.IsKeyword("CONSTRAINT") ||
                   context.IsKeyword("PRIMARY") ||
                   context.IsKeyword("UNIQUE") ||
                   context.IsKeyword("FOREIGN") ||
                   context.IsKeyword("CHECK");
        }

        private static bool IsAddConstraintStart(IParserContext context)
        {
            return context.IsKeyword("CONSTRAINT") ||
                   context.IsKeyword("FOREIGN") ||
                   context.IsKeyword("CHECK");
        }

        private static bool IsCreateIndexStatement(IParserContext context)
        {
            if (!context.IsKeyword("CREATE"))
            {
                return false;
            }

            var first = context.PeekToken(1).Value;
            var second = context.PeekToken(2).Value;

            if (first.Equals("INDEX", StringComparison.OrdinalIgnoreCase) ||
                first.Equals("KEY", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (first.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                first.Equals("FULLTEXT", StringComparison.OrdinalIgnoreCase) ||
                first.Equals("SPATIAL", StringComparison.OrdinalIgnoreCase))
            {
                return second.Equals("INDEX", StringComparison.OrdinalIgnoreCase) ||
                       second.Equals("KEY", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsDropIndexStatement(IParserContext context)
        {
            return context.IsKeyword("DROP") &&
                   context.PeekToken(1).Value.Equals("INDEX", StringComparison.OrdinalIgnoreCase);
        }

        private List<TableOption> ParseTableOptions(IParserContext context)
        {
            var options = new List<TableOption>();
            bool keepParsing = true;
            while (keepParsing && context.PeekToken().Type == TokenType.Keyword)
            {
                string name = null;
                if (context.IsKeyword("ENGINE"))
                {
                    context.ConsumeToken("ENGINE");
                    name = "ENGINE";
                }
                else if (context.IsKeyword("AUTO_INCREMENT"))
                {
                    context.ConsumeToken("AUTO_INCREMENT");
                    name = "AUTO_INCREMENT";
                }
                else if (context.IsKeyword("DEFAULT"))
                {
                    context.ConsumeToken("DEFAULT");
                    if (context.IsKeyword("CHARACTER"))
                    {
                        context.ConsumeToken("CHARACTER");
                        context.ConsumeToken("SET");
                        name = "DEFAULT CHARACTER SET";
                    }
                    else if (context.IsKeyword("CHARSET"))
                    {
                        context.ConsumeToken("CHARSET");
                        name = "DEFAULT CHARSET";
                    }
                    else if (context.IsKeyword("COLLATE"))
                    {
                        context.ConsumeToken("COLLATE");
                        name = "DEFAULT COLLATE";
                    }
                }
                else if (context.IsKeyword("CHARACTER"))
                {
                    context.ConsumeToken("CHARACTER");
                    context.ConsumeToken("SET");
                    name = "CHARACTER SET";
                }
                else if (context.IsKeyword("CHARSET"))
                {
                    context.ConsumeToken("CHARSET");
                    name = "CHARSET";
                }
                else if (context.IsKeyword("COLLATE"))
                {
                    context.ConsumeToken("COLLATE");
                    name = "COLLATE";
                }
                else if (context.IsKeyword("COMMENT"))
                {
                    context.ConsumeToken("COMMENT");
                    name = "COMMENT";
                }
                else
                {
                    keepParsing = false;
                }

                if (!keepParsing || name == null)
                {
                    break;
                }

                if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "=")
                {
                    context.ConsumeToken("=", TokenType.Operator);
                }

                var valueToken = context.ConsumeToken();
                var option = new TableOption
                {
                    Name = name
                };

                if (valueToken.Type == TokenType.StringLiteral)
                {
                    option.Value = valueToken.Value;
                    option.IsStringValue = true;
                }
                else
                {
                    option.Value = valueToken.Value;
                }

                options.Add(option);
            }

            return options;
        }

        private static bool IsIndexStart(IParserContext context)
        {
            return context.IsKeyword("CONSTRAINT") ||
                   context.IsKeyword("PRIMARY") ||
                   context.IsKeyword("UNIQUE") ||
                   context.IsKeyword("INDEX") ||
                   context.IsKeyword("KEY") ||
                   context.IsKeyword("FULLTEXT") ||
                   context.IsKeyword("SPATIAL");
        }

        private List<ColumnExpression> ParseConstraintColumns(IParserContext context)
        {
            context.ConsumeToken("(", TokenType.Parenthesis);
            var columns = new List<ColumnExpression>();
            do
            {
                var colToken = context.ConsumeToken();
                if (colToken.Type != TokenType.Identifier && colToken.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected column name in constraint.", colToken.StartIndex);
                }

                columns.Add(new ColumnExpression
                {
                    ColumnName = colToken.Value,
                    ColumnNameQuoted = colToken.IsQuoted,
                    ColumnQuoteStyle = colToken.QuoteStyle
                });
            } while (context.MatchToken(",", TokenType.Comma));
            context.ConsumeToken(")", TokenType.Parenthesis);
            return columns;
        }

        private string ParseParenthesizedTokens(IParserContext context)
        {
            context.ConsumeToken("(", TokenType.Parenthesis);
            int depth = 1;
            var parts = new List<string> { "(" };

            while (depth > 0)
            {
                var token = context.ConsumeToken();
                if (token.Type == TokenType.Parenthesis && token.Value == "(")
                {
                    depth++;
                }
                else if (token.Type == TokenType.Parenthesis && token.Value == ")")
                {
                    depth--;
                }

                parts.Add(token.Value);
            }

            return string.Join("", parts);
        }

        private OrderByClause ParseOrderByClause(IParserContext context)
        {
            var orderBy = new OrderByClause();
            do
            {
                var item = new OrderItem { Expression = _expressionParser.Parse(context), IsAscending = true };
                if (context.IsKeyword("ASC"))
                {
                    context.ConsumeToken("ASC");
                    item.IsAscending = true;
                }
                else if (context.IsKeyword("DESC"))
                {
                    context.ConsumeToken("DESC");
                    item.IsAscending = false;
                }

                orderBy.OrderItems.Add(item);
            } while (context.MatchToken(",", TokenType.Comma));

            return orderBy;
        }

        private LimitClause ParseLimitClause(IParserContext context)
        {
            context.ConsumeToken("LIMIT");
            var firstExpr = _expressionParser.Parse(context);
            var limit = new LimitClause();

            if (context.MatchToken(",", TokenType.Comma))
            {
                limit.OffsetExpression = firstExpr;
                limit.CountExpression = _expressionParser.Parse(context);
                return limit;
            }

            if (context.IsKeyword("OFFSET"))
            {
                context.ConsumeToken("OFFSET");
                limit.CountExpression = firstExpr;
                limit.OffsetExpression = _expressionParser.Parse(context);
                return limit;
            }

            limit.CountExpression = firstExpr;
            return limit;
        }

        private static bool IsValueToken(Token token)
        {
            return (token.Type == TokenType.Identifier || token.Type == TokenType.Keyword) &&
                   token.Value.Equals("VALUE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
