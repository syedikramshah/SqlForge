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
    /// for the SQL Anywhere dialect.
    /// </summary>
    public class SqlAnywhereStatementParserFactory : IStatementParserFactory
    {
        private readonly SelectStatementParser _selectStatementParser;
        private readonly IExpressionParser _expressionParser;

        public SqlAnywhereStatementParserFactory(SelectStatementParser selectStatementParser, IExpressionParser expressionParser)
        {
            _selectStatementParser = selectStatementParser;
            _expressionParser = expressionParser;
        }

        public SqlStatement ParseStatement(IParserContext context)
        {
            ISqlNode statementBody = ParseSingleStatementBody(context);

            // Handle set operators (UNION, EXCEPT, INTERSECT) with left-associativity.
            // Each iteration takes the current accumulated result as the left side
            // and parses only a single statement for the right side.
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
                        throw new SqlParseException($"{upper} ALL is not supported in SQL Anywhere.", context.PeekToken().StartIndex);
                    }
                }

                // Parse only a single statement body for the right side.
                // This ensures left-associativity: A UNION B EXCEPT C becomes ((A UNION B) EXCEPT C)
                // If we called ParseStatement here, it would consume B EXCEPT C as a unit, giving wrong associativity.
                ISqlNode right = ParseSingleStatementBody(context);

                var setOpExpr = new SetOperatorExpression
                {
                    Operator = opType,
                    Left = statementBody,
                    Right = right
                };

                statementBody = setOpExpr;
            }

            if (statementBody is SelectStatement || statementBody is SetOperatorExpression)
            {
                return new SqlStatement
                {
                    Type = StatementType.Select,
                    Body = statementBody
                };
            }

            if (statementBody is InsertStatement)
            {
                return new SqlStatement
                {
                    Type = StatementType.Insert,
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

        /// <summary>
        /// Parses a single statement body: either a SELECT statement or a parenthesized expression.
        /// Does NOT handle chained set operators at this level - that's done by ParseStatement.
        /// Parenthesized expressions can contain set operators within them (as a grouped unit).
        /// </summary>
        private ISqlNode ParseSingleStatementBody(IParserContext context)
        {
            if (_selectStatementParser.CanParse(context))
            {
                return _selectStatementParser.Parse(context).Body;
            }

            if (context.IsKeyword("INSERT"))
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
            context.ConsumeToken("INSERT");
            var insert = new InsertStatement();

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

            if (context.IsKeyword("VALUES"))
            {
                context.ConsumeToken("VALUES");
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
                throw new SqlParseException("Expected VALUES or SELECT in INSERT statement.", context.PeekToken().StartIndex);
            }

            return insert;
        }

        private UpdateStatement ParseUpdateStatement(IParserContext context)
        {
            context.ConsumeToken("UPDATE");
            var update = new UpdateStatement
            {
                Target = ParseTableName(context)
            };

            context.ConsumeToken("SET");
            update.SetClauses = ParseAssignments(context);

            if (context.IsKeyword("WHERE"))
            {
                context.ConsumeToken("WHERE");
                update.WhereClause = new WhereClause { Condition = _expressionParser.Parse(context) };
            }

            return update;
        }

        private DeleteStatement ParseDeleteStatement(IParserContext context)
        {
            context.ConsumeToken("DELETE");
            var delete = new DeleteStatement();

            if (context.IsKeyword("FROM"))
            {
                context.ConsumeToken("FROM");
            }

            delete.Target = ParseTableName(context);

            if (context.IsKeyword("WHERE"))
            {
                context.ConsumeToken("WHERE");
                delete.WhereClause = new WhereClause { Condition = _expressionParser.Parse(context) };
            }

            return delete;
        }

        private CreateTableStatement ParseCreateTableStatement(IParserContext context)
        {
            context.ConsumeToken("CREATE");
            var create = new CreateTableStatement();

            context.ConsumeToken("TABLE");
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
                if (IsTableConstraintStart(context))
                {
                    return new AlterTableAction
                    {
                        ActionType = AlterTableActionType.AddConstraint,
                        Constraint = ParseTableConstraint(context)
                    };
                }
                if (context.IsKeyword("COLUMN"))
                {
                    context.ConsumeToken("COLUMN");
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

            if (context.IsKeyword("ALTER"))
            {
                context.ConsumeToken("ALTER");
                if (context.IsKeyword("COLUMN"))
                {
                    context.ConsumeToken("COLUMN");
                }

                return new AlterTableAction
                {
                    ActionType = AlterTableActionType.AlterColumn,
                    Column = ParseColumnDefinition(context)
                };
            }

            if (context.IsKeyword("RENAME"))
            {
                context.ConsumeToken("RENAME");
                if (context.IsKeyword("TO"))
                {
                    context.ConsumeToken("TO");
                    var nameToken = context.ConsumeToken();
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
            var column = new ColumnDefinition
            {
                Name = nameToken.Value,
                NameQuoted = nameToken.IsQuoted,
                NameQuoteStyle = nameToken.QuoteStyle
            };

            var typeToken = context.ConsumeToken();
            string dataType = typeToken.Value;
            if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
            {
                dataType += ParseParenthesizedTokens(context);
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

                if (context.IsKeyword("IDENTITY"))
                {
                    context.ConsumeToken("IDENTITY");
                    column.IsIdentity = true;
                    if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
                    {
                        ParseIdentitySeedIncrement(context, column);
                    }
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
                    column.IsUnique = true;
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

        private List<ColumnExpression> ParseConstraintColumns(IParserContext context)
        {
            context.ConsumeToken("(", TokenType.Parenthesis);
            var columns = new List<ColumnExpression>();
            do
            {
                var colToken = context.ConsumeToken();
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

        private void ParseIdentitySeedIncrement(IParserContext context, ColumnDefinition column)
        {
            context.ConsumeToken("(", TokenType.Parenthesis);
            column.IdentitySeed = _expressionParser.Parse(context);
            if (context.MatchToken(",", TokenType.Comma))
            {
                column.IdentityIncrement = _expressionParser.Parse(context);
            }
            context.ConsumeToken(")", TokenType.Parenthesis);
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

        private CreateIndexStatement ParseCreateIndexStatement(IParserContext context)
        {
            context.ConsumeToken("CREATE");
            var index = new IndexDefinition { Type = IndexType.Index };

            if (context.IsKeyword("UNIQUE"))
            {
                context.ConsumeToken("UNIQUE");
                index.Type = IndexType.Unique;
            }

            context.ConsumeToken("INDEX");
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
            index.Columns = ParseIndexColumns(context);

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

            if (context.IsKeyword("IF"))
            {
                context.ConsumeToken("IF");
                context.ConsumeToken("EXISTS");
                drop.IfExists = true;
            }

            var nameToken = context.ConsumeToken();
            if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
            {
                throw new SqlParseException("Expected index name.", nameToken.StartIndex);
            }

            drop.IndexName = nameToken.Value;
            drop.IndexNameQuoted = nameToken.IsQuoted;
            drop.IndexNameQuoteStyle = nameToken.QuoteStyle;

            if (context.IsKeyword("ON"))
            {
                context.ConsumeToken("ON");
                drop.Target = ParseTableName(context);
            }

            return drop;
        }

        private List<IndexColumn> ParseIndexColumns(IParserContext context)
        {
            context.ConsumeToken("(", TokenType.Parenthesis);
            var columns = new List<IndexColumn>();
            do
            {
                var column = ParseColumnReference(context);
                var indexColumn = new IndexColumn { Column = column };

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

        private static bool IsCreateIndexStatement(IParserContext context)
        {
            if (!context.IsKeyword("CREATE"))
            {
                return false;
            }

            var first = context.PeekToken(1).Value;
            var second = context.PeekToken(2).Value;
            if (first.Equals("INDEX", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return first.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase) &&
                   second.Equals("INDEX", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDropIndexStatement(IParserContext context)
        {
            return context.IsKeyword("DROP") &&
                   context.PeekToken(1).Value.Equals("INDEX", StringComparison.OrdinalIgnoreCase);
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
    }
}
