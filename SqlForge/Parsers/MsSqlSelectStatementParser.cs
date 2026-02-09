using System;
using System.Collections.Generic;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{
    public class MsSqlSelectStatementParser : IStatementParser
    {
        private readonly IExpressionParser _expressionParser;
        private readonly IStatementParserFactory _statementParserFactory;

        public MsSqlSelectStatementParser(IExpressionParser expressionParser, IStatementParserFactory statementParserFactory)
        {
            _expressionParser = expressionParser;
            _statementParserFactory = statementParserFactory;
        }

        public bool CanParse(IParserContext context)
        {
            return context.IsKeyword("SELECT");
        }

        public SqlStatement Parse(IParserContext context)
        {
            context.ConsumeToken("SELECT");
            var select = new SelectStatement();

            if (context.MatchToken("DISTINCT"))
            {
                select.IsDistinct = true;
            }

            if (context.IsKeyword("TOP"))
            {
                select.TopClause = ParseTopClause(context);
            }

            select.SelectItems = ParseSelectItems(context);

            if (context.IsKeyword("FROM"))
            {
                context.ConsumeToken("FROM");
                select.FromClause = ParseFromClause(context);
            }

            if (context.IsKeyword("WHERE"))
            {
                context.ConsumeToken("WHERE");
                select.WhereClause = ParseWhereClause(context);
            }

            if (context.IsKeyword("GROUP"))
            {
                context.ConsumeToken("GROUP");
                context.ConsumeToken("BY");
                select.GroupByClause = ParseGroupByClause(context);
            }

            if (context.IsKeyword("HAVING"))
            {
                context.ConsumeToken("HAVING");
                select.HavingClause = ParseHavingClause(context);
            }

            if (context.IsKeyword("ORDER"))
            {
                context.ConsumeToken("ORDER");
                context.ConsumeToken("BY");
                select.OrderByClause = ParseOrderByClause(context);
            }

            if (context.IsKeyword("OFFSET"))
            {
                if (select.OrderByClause == null)
                {
                    throw new SqlParseException("OFFSET/FETCH requires an ORDER BY clause in MS SQL Server", context.PeekToken().StartIndex);
                }

                select.OffsetFetchClause = ParseOffsetFetchClause(context);
            }

            if (select.TopClause != null && select.OffsetFetchClause != null)
            {
                throw new SqlParseException("TOP and OFFSET/FETCH cannot be used together in the same SELECT statement");
            }

            if (select.TopClause != null && select.TopClause.WithTies && select.OrderByClause == null)
            {
                throw new SqlParseException("TOP WITH TIES requires an ORDER BY clause");
            }

            return new SqlStatement
            {
                Type = StatementType.Select,
                Body = select
            };
        }

        private TopClause ParseTopClause(IParserContext context)
        {
            context.ConsumeToken("TOP");
            var topClause = new TopClause
            {
                Expression = _expressionParser.Parse(context)
            };

            if (context.IsKeyword("PERCENT"))
            {
                context.ConsumeToken("PERCENT");
                topClause.IsPercent = true;
            }

            if (context.IsKeyword("WITH") && context.PeekToken(1).Type == TokenType.Keyword && context.PeekToken(1).Value.Equals("TIES", StringComparison.OrdinalIgnoreCase))
            {
                context.ConsumeToken("WITH");
                context.ConsumeToken("TIES");
                topClause.WithTies = true;
            }

            return topClause;
        }

        private OffsetFetchClause ParseOffsetFetchClause(IParserContext context)
        {
            context.ConsumeToken("OFFSET");
            var clause = new OffsetFetchClause
            {
                OffsetExpression = _expressionParser.Parse(context)
            };

            if (!context.IsKeyword("ROW") && !context.IsKeyword("ROWS"))
            {
                throw new SqlParseException("Expected ROW or ROWS after OFFSET expression", context.PeekToken().StartIndex);
            }

            context.ConsumeToken();

            if (context.IsKeyword("FETCH"))
            {
                context.ConsumeToken("FETCH");
                if (context.IsKeyword("NEXT"))
                {
                    context.ConsumeToken("NEXT");
                    clause.IsNext = true;
                }
                else if (context.IsKeyword("FIRST"))
                {
                    context.ConsumeToken("FIRST");
                    clause.IsNext = false;
                }
                else
                {
                    throw new SqlParseException("Expected NEXT or FIRST after FETCH", context.PeekToken().StartIndex);
                }

                clause.FetchExpression = _expressionParser.Parse(context);

                if (!context.IsKeyword("ROW") && !context.IsKeyword("ROWS"))
                {
                    throw new SqlParseException("Expected ROW or ROWS after FETCH expression", context.PeekToken().StartIndex);
                }

                context.ConsumeToken();
                context.ConsumeToken("ONLY");
            }

            return clause;
        }

        private List<SelectExpression> ParseSelectItems(IParserContext context)
        {
            var selectItems = new List<SelectExpression>();
            do
            {
                ISqlNode expression;
                if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "*")
                {
                    expression = new ColumnExpression { ColumnName = "*" };
                    context.ConsumeToken("*", TokenType.Operator);
                }
                else
                {
                    expression = _expressionParser.Parse(context);
                }

                var selectExpr = new SelectExpression { Expression = expression };
                if (IsAsToken(context.PeekToken()))
                {
                    context.ConsumeToken();
                    if (context.PeekToken().Type != TokenType.Identifier)
                    {
                        throw new SqlParseException("Expected identifier after AS.", context.PeekToken().StartIndex);
                    }

                    Token aliasToken = context.ConsumeToken();
                    selectExpr.Alias = aliasToken.Value;
                    selectExpr.AliasQuoted = aliasToken.IsQuoted;
                    selectExpr.AliasQuoteStyle = aliasToken.QuoteStyle;
                    selectExpr.HasExplicitAs = true;
                }
                else if (context.PeekToken().Type == TokenType.Identifier && !context.IsKeyword(context.PeekToken().Value))
                {
                    Token aliasToken = context.ConsumeToken();
                    selectExpr.Alias = aliasToken.Value;
                    selectExpr.AliasQuoted = aliasToken.IsQuoted;
                    selectExpr.AliasQuoteStyle = aliasToken.QuoteStyle;
                    selectExpr.HasExplicitAs = false;
                }

                selectItems.Add(selectExpr);
            } while (context.MatchToken(",", TokenType.Comma));

            return selectItems;
        }

        private FromClause ParseFromClause(IParserContext context)
        {
            var fromClause = new FromClause();
            ISqlNode currentFromNode = ParseTableExpression(context);

            while (IsJoinKeyword(context.PeekToken()) || IsApplyKeyword(context))
            {
                if (IsApplyKeyword(context))
                {
                    var applyToken = context.ConsumeToken();
                    context.ConsumeToken("APPLY");
                    ISqlNode rightNode = ParseTableExpression(context);

                    currentFromNode = new JoinExpression
                    {
                        Left = currentFromNode,
                        Type = applyToken.Value.Equals("CROSS", StringComparison.OrdinalIgnoreCase) ? JoinType.CrossApply : JoinType.OuterApply,
                        Right = rightNode,
                        OnCondition = null
                    };
                }
                else
                {
                    var joinTypeToken = context.ConsumeToken();
                    if (context.IsKeyword("OUTER"))
                    {
                        context.ConsumeToken("OUTER");
                    }

                    context.ConsumeToken("JOIN");
                    ISqlNode rightNode = ParseTableExpression(context);

                    ISqlNode onCondition = null;
                    if (GetJoinType(joinTypeToken.Value) != JoinType.Cross)
                    {
                        context.ConsumeToken("ON");
                        onCondition = _expressionParser.Parse(context);
                    }

                    currentFromNode = new JoinExpression
                    {
                        Left = currentFromNode,
                        Type = GetJoinType(joinTypeToken.Value),
                        Right = rightNode,
                        OnCondition = onCondition
                    };
                }
            }

            fromClause.TableExpressions.Add(currentFromNode);
            return fromClause;
        }

        private List<TableHint> ParseTableHints(IParserContext context)
        {
            context.ConsumeToken("WITH");
            context.ConsumeToken("(", TokenType.Parenthesis);
            var hints = new List<TableHint>();

            do
            {
                var hintToken = context.ConsumeToken();
                if (hintToken.Type != TokenType.Identifier && hintToken.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected table hint name.", hintToken.StartIndex);
                }

                var hint = new TableHint { HintName = hintToken.Value };

                if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
                {
                    context.ConsumeToken("(", TokenType.Parenthesis);
                    if (!(context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == ")"))
                    {
                        do
                        {
                            hint.Parameters.Add(_expressionParser.Parse(context));
                        } while (context.MatchToken(",", TokenType.Comma));
                    }

                    context.ConsumeToken(")", TokenType.Parenthesis);
                }

                hints.Add(hint);
            } while (context.MatchToken(",", TokenType.Comma));

            context.ConsumeToken(")", TokenType.Parenthesis);
            return hints;
        }

        private ISqlNode ParseTableExpression(IParserContext context)
        {
            if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
            {
                context.ConsumeToken("(", TokenType.Parenthesis);
                var subqueryExpr = new SubqueryExpression
                {
                    SubqueryStatement = _statementParserFactory.ParseStatement(context)
                };
                context.ConsumeToken(")", TokenType.Parenthesis);

                if (context.MatchToken("AS"))
                {
                }

                if (context.PeekToken().Type != TokenType.Identifier)
                {
                    throw new SqlParseException("Subquery in FROM clause must have an alias.", context.PeekToken().StartIndex);
                }

                Token aliasToken = context.ConsumeToken();
                subqueryExpr.Alias = aliasToken.Value;
                subqueryExpr.AliasQuoted = aliasToken.IsQuoted;
                subqueryExpr.AliasQuoteStyle = aliasToken.QuoteStyle;
                return subqueryExpr;
            }

            var table = new TableExpression();
            Token id1 = context.ConsumeToken();
            table.TableQuoteStyle = id1.QuoteStyle;

            if (context.PeekToken().Value == ".")
            {
                context.ConsumeToken(".", TokenType.Operator);
                Token id2 = context.ConsumeToken(context.PeekToken().Value, TokenType.Identifier);

                table.SchemaName = id1.Value;
                table.SchemaNameQuoted = id1.IsQuoted;
                table.SchemaQuoteStyle = id1.QuoteStyle;

                table.TableName = id2.Value;
                table.TableNameQuoted = id2.IsQuoted;
                table.TableQuoteStyle = id2.QuoteStyle;
            }
            else
            {
                table.TableName = id1.Value;
                table.TableNameQuoted = id1.IsQuoted;
            }

            if (context.IsKeyword("WITH"))
            {
                table.TableHints = ParseTableHints(context);
            }

            if (context.MatchToken("AS"))
            {
                if (context.PeekToken().Type != TokenType.Identifier)
                {
                    throw new SqlParseException("Expected alias after AS.", context.PeekToken().StartIndex);
                }

                Token aliasToken = context.ConsumeToken();
                table.Alias = aliasToken.Value;
                table.AliasQuoted = aliasToken.IsQuoted;
                table.AliasQuoteStyle = aliasToken.QuoteStyle;
                table.HasExplicitAs = true;
            }
            else if (context.PeekToken().Type == TokenType.Identifier && !context.IsKeyword(context.PeekToken().Value))
            {
                Token aliasToken = context.ConsumeToken();
                table.Alias = aliasToken.Value;
                table.AliasQuoted = aliasToken.IsQuoted;
                table.AliasQuoteStyle = aliasToken.QuoteStyle;
                table.HasExplicitAs = false;
            }

            return table;
        }

        private WhereClause ParseWhereClause(IParserContext context)
        {
            return new WhereClause { Condition = _expressionParser.Parse(context) };
        }

        private GroupByClause ParseGroupByClause(IParserContext context)
        {
            var groupByClause = new GroupByClause();
            do
            {
                groupByClause.GroupingExpressions.Add(_expressionParser.Parse(context));
            } while (context.MatchToken(",", TokenType.Comma));

            return groupByClause;
        }

        private HavingClause ParseHavingClause(IParserContext context)
        {
            return new HavingClause { Condition = _expressionParser.Parse(context) };
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

        private bool IsJoinKeyword(Token token)
        {
            return token.Type == TokenType.Keyword &&
                   (token.Value.Equals("INNER", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("LEFT", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("RIGHT", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("FULL", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("CROSS", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("NATURAL", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAsToken(Token token)
        {
            return token != null && token.Value.Equals("AS", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsApplyKeyword(IParserContext context)
        {
            var token = context.PeekToken();
            if (token.Type != TokenType.Keyword)
            {
                return false;
            }

            if (!token.Value.Equals("CROSS", StringComparison.OrdinalIgnoreCase) &&
                !token.Value.Equals("OUTER", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var nextToken = context.PeekToken(1);
            return nextToken != null &&
                   nextToken.Type == TokenType.Keyword &&
                   nextToken.Value.Equals("APPLY", StringComparison.OrdinalIgnoreCase);
        }

        private JoinType GetJoinType(string keyword)
        {
            string upperKeyword = keyword.ToUpperInvariant();
            if (upperKeyword == "INNER") return JoinType.Inner;
            if (upperKeyword == "LEFT") return JoinType.Left;
            if (upperKeyword == "RIGHT") return JoinType.Right;
            if (upperKeyword == "FULL") return JoinType.Full;
            if (upperKeyword == "CROSS") return JoinType.Cross;
            if (upperKeyword == "NATURAL") return JoinType.Natural;
            if (upperKeyword == "OUTER") return JoinType.OuterApply;
            return JoinType.Unknown;
        }
    }
}
