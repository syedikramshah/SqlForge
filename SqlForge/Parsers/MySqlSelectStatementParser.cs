using System;
using System.Collections.Generic;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{
    public class MySqlSelectStatementParser : IStatementParser
    {
        private readonly IExpressionParser _expressionParser;
        private readonly IStatementParserFactory _statementParserFactory;

        public MySqlSelectStatementParser(IExpressionParser expressionParser, IStatementParserFactory statementParserFactory)
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

            ParseSelectModifiers(context, select);

            select.SelectItems = ParseSelectItems(context);

            if (context.IsKeyword("INTO"))
            {
                select.IntoClause = ParseSelectIntoClause(context);
            }

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

                if (context.IsKeyword("WITH") && context.PeekToken(1).Type == TokenType.Keyword &&
                    context.PeekToken(1).Value.Equals("ROLLUP", StringComparison.OrdinalIgnoreCase))
                {
                    context.ConsumeToken("WITH");
                    context.ConsumeToken("ROLLUP");
                    select.GroupByClause.WithRollup = true;
                }
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

            if (context.IsKeyword("LIMIT"))
            {
                select.LimitClause = ParseLimitClause(context);
            }

            if (context.IsKeyword("FOR") && context.PeekToken(1).Type == TokenType.Keyword &&
                context.PeekToken(1).Value.Equals("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                context.ConsumeToken("FOR");
                context.ConsumeToken("UPDATE");
                select.ForUpdate = true;
            }

            if (context.IsKeyword("LOCK") && context.PeekToken(1).Type == TokenType.Keyword &&
                context.PeekToken(1).Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
            {
                context.ConsumeToken("LOCK");
                context.ConsumeToken("IN");
                context.ConsumeToken("SHARE");
                context.ConsumeToken("MODE");
                select.LockInShareMode = true;
            }

            return new SqlStatement
            {
                Type = StatementType.Select,
                Body = select
            };
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

        private void ParseSelectModifiers(IParserContext context, SelectStatement select)
        {
            bool keepParsing = true;
            while (keepParsing)
            {
                if (context.MatchToken("DISTINCT"))
                {
                    select.IsDistinct = true;
                    continue;
                }

                if (context.MatchToken("DISTINCTROW"))
                {
                    select.IsDistinctRow = true;
                    continue;
                }

                var token = context.PeekToken();
                if (IsSelectModifier(token))
                {
                    select.SelectModifiers.Add(token.Value.ToUpperInvariant());
                    context.ConsumeToken();
                    continue;
                }

                keepParsing = false;
            }
        }

        private SelectIntoClause ParseSelectIntoClause(IParserContext context)
        {
            context.ConsumeToken("INTO");
            if (context.IsKeyword("OUTFILE"))
            {
                context.ConsumeToken("OUTFILE");
                var fileToken = context.ConsumeToken();
                if (fileToken.Type != TokenType.StringLiteral)
                {
                    throw new SqlParseException("Expected string literal after INTO OUTFILE.", fileToken.StartIndex);
                }

                return new SelectIntoClause { Type = SelectIntoType.Outfile, FilePath = fileToken.Value };
            }

            if (context.IsKeyword("DUMPFILE"))
            {
                context.ConsumeToken("DUMPFILE");
                var fileToken = context.ConsumeToken();
                if (fileToken.Type != TokenType.StringLiteral)
                {
                    throw new SqlParseException("Expected string literal after INTO DUMPFILE.", fileToken.StartIndex);
                }

                return new SelectIntoClause { Type = SelectIntoType.Dumpfile, FilePath = fileToken.Value };
            }

            throw new SqlParseException("Expected OUTFILE or DUMPFILE after INTO.", context.PeekToken().StartIndex);
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

            while (IsJoinKeyword(context.PeekToken()))
            {
                var joinTypeToken = context.ConsumeToken();
                var joinType = GetJoinType(joinTypeToken.Value);
                if (joinType != JoinType.Straight)
                {
                    if (context.IsKeyword("OUTER"))
                    {
                        context.ConsumeToken("OUTER");
                    }

                    context.ConsumeToken("JOIN");
                }

                ISqlNode rightNode = ParseTableExpression(context);

                ISqlNode onCondition = null;
                if (joinType != JoinType.Cross && joinType != JoinType.Natural)
                {
                    context.ConsumeToken("ON");
                    onCondition = _expressionParser.Parse(context);
                }

                currentFromNode = new JoinExpression
                {
                    Left = currentFromNode,
                    Type = joinType,
                    Right = rightNode,
                    OnCondition = onCondition
                };
            }

            fromClause.TableExpressions.Add(currentFromNode);
            return fromClause;
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

        private bool IsAsToken(Token token)
        {
            return token.Type == TokenType.Keyword && token.Value.Equals("AS", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsJoinKeyword(Token token)
        {
            return token.Type == TokenType.Keyword &&
                   (token.Value.Equals("INNER", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("LEFT", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("RIGHT", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("FULL", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("CROSS", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("NATURAL", StringComparison.OrdinalIgnoreCase) ||
                    token.Value.Equals("STRAIGHT_JOIN", StringComparison.OrdinalIgnoreCase));
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
            if (upperKeyword == "STRAIGHT_JOIN") return JoinType.Straight;
            return JoinType.Unknown;
        }

        private bool IsSelectModifier(Token token)
        {
            if (token.Type != TokenType.Keyword)
            {
                return false;
            }

            string value = token.Value.ToUpperInvariant();
            return value == "HIGH_PRIORITY" ||
                   value == "STRAIGHT_JOIN" ||
                   value == "SQL_SMALL_RESULT" ||
                   value == "SQL_BIG_RESULT" ||
                   value == "SQL_BUFFER_RESULT" ||
                   value == "SQL_CACHE" ||
                   value == "SQL_NO_CACHE" ||
                   value == "SQL_CALC_FOUND_ROWS";
        }
    }
}
