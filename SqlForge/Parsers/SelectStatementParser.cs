using System;
using System.Collections.Generic;
using System.Linq;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{
    /// <summary>
    /// Parses a SELECT statement and its clauses.
    /// </summary>
    public class SelectStatementParser : IStatementParser
    {
        private readonly IExpressionParser _expressionParser;
        private readonly IStatementParserFactory _statementParserFactory; // Now inject the factory

        // Constructor to inject dependencies
        public SelectStatementParser(IExpressionParser expressionParser, IStatementParserFactory statementParserFactory)
        {
            _expressionParser = expressionParser;
            _statementParserFactory = statementParserFactory; // Store the factory
        }

        /// <summary>
        /// Checks if the current context starts with a SELECT keyword.
        /// This method implements IStatementParser.CanParse.
        /// </summary>
        public bool CanParse(IParserContext context) // <--- ADD THIS METHOD
        {
            return context.IsKeyword("SELECT");
        }

        /// <summary>
        /// Parses the full SELECT statement and its optional clauses.
        /// </summary>
        public SqlStatement Parse(IParserContext context)
        {
            context.ConsumeToken("SELECT");
            var select = new SelectStatement();

            if (context.MatchToken("DISTINCT"))
                select.IsDistinct = true;

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

            return new SqlStatement
            {
                Type = StatementType.Select,
                Body = select
            };
        }

        // --- Helper Parsing Methods (rest of the class) ---
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
                if (context.MatchToken("AS"))
                {
                    if (context.PeekToken().Type != TokenType.Identifier)
                        throw new SqlParseException("Expected identifier after AS.", context.PeekToken().StartIndex);
                    Token aliasToken = context.ConsumeToken();
                    selectExpr.Alias = aliasToken.Value;
                    selectExpr.AliasQuoted = aliasToken.IsQuoted;
                    selectExpr.HasExplicitAs = true;
                }
                else if (context.PeekToken().Type == TokenType.Identifier && !context.IsKeyword(context.PeekToken().Value))
                {
                    Token aliasToken = context.ConsumeToken();
                    selectExpr.Alias = aliasToken.Value;
                    selectExpr.AliasQuoted = aliasToken.IsQuoted;
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
                if (context.IsKeyword("OUTER"))
                    context.ConsumeToken("OUTER");
                context.ConsumeToken("JOIN");
                ISqlNode rightNode = ParseTableExpression(context);
                context.ConsumeToken("ON");
                var onCondition = _expressionParser.Parse(context);
                currentFromNode = new JoinExpression
                {
                    Left = currentFromNode,
                    Type = GetJoinType(joinTypeToken.Value),
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
                if (context.MatchToken("AS")) { }
                if (context.PeekToken().Type != TokenType.Identifier)
                    throw new SqlParseException("Subquery in FROM clause must have an alias.", context.PeekToken().StartIndex);
                Token aliasToken = context.ConsumeToken();
                subqueryExpr.Alias = aliasToken.Value;
                subqueryExpr.AliasQuoted = aliasToken.IsQuoted;
                return subqueryExpr;
            }
            var table = new TableExpression();
            Token id1 = context.ConsumeToken();
            bool id1Quoted = id1.IsQuoted;
            if (context.PeekToken().Value == ".")
            {
                context.ConsumeToken(".", TokenType.Operator);
                Token id2 = context.ConsumeToken(context.PeekToken().Value, TokenType.Identifier);
                bool id2Quoted = id2.IsQuoted;
                table.SchemaName = id1.Value;
                table.SchemaNameQuoted = id1Quoted;
                table.TableName = id2.Value;
                table.TableNameQuoted = id2Quoted;
            }
            else
            {
                table.TableName = id1.Value;
                table.TableNameQuoted = id1Quoted;
            }
            if (context.MatchToken("AS"))
            {
                if (context.PeekToken().Type != TokenType.Identifier)
                    throw new SqlParseException("Expected alias after AS.", context.PeekToken().StartIndex);
                Token aliasToken = context.ConsumeToken();
                table.Alias = aliasToken.Value;
                table.AliasQuoted = aliasToken.IsQuoted;
                table.HasExplicitAs = true;
            }
            else if (context.PeekToken().Type == TokenType.Identifier && !context.IsKeyword(context.PeekToken().Value))
            {
                Token aliasToken = context.ConsumeToken();
                table.Alias = aliasToken.Value;
                table.AliasQuoted = aliasToken.IsQuoted;
                table.HasExplicitAs = false;
            }
            return table;
        }

        private WhereClause ParseWhereClause(IParserContext context)
        {
            var whereClause = new WhereClause();
            whereClause.Condition = _expressionParser.Parse(context);
            return whereClause;
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
            var havingClause = new HavingClause();
            havingClause.Condition = _expressionParser.Parse(context);
            return havingClause;
        }

        private OrderByClause ParseOrderByClause(IParserContext context)
        {
            var orderBy = new OrderByClause();
            do
            {
                var item = new OrderItem { Expression = _expressionParser.Parse(context) };
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

        // Helper methods specific to this parser (can be moved to a utility if shared)
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

        private JoinType GetJoinType(string keyword)
        {
            string upperKeyword = keyword.ToUpperInvariant();
            if (upperKeyword == "INNER") return JoinType.Inner;
            else if (upperKeyword == "LEFT") return JoinType.Left;
            else if (upperKeyword == "RIGHT") return JoinType.Right;
            else if (upperKeyword == "FULL") return JoinType.Full;
            else if (upperKeyword == "CROSS") return JoinType.Cross;
            else if (upperKeyword == "NATURAL") return JoinType.Natural;
            else return JoinType.Unknown;
        }
    }
}