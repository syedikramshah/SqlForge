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
    /// Parses SQL expressions for the SQL Anywhere dialect.
    /// </summary>
    public class SqlAnywhereExpressionParser : IExpressionParser
    {
        private readonly IStatementParserFactory _statementParserFactory;

        public SqlAnywhereExpressionParser(IStatementParserFactory statementParserFactory)
        {
            _statementParserFactory = statementParserFactory;
        }


        /// <summary>
        /// Parses a general SQL expression, handling operator precedence.
        /// </summary>
        public ISqlNode Parse(IParserContext context)
        {
            ISqlNode left = ParseAndExpression(context);

            while (context.IsKeyword("OR"))
            {
                var opToken = context.ConsumeToken("OR");
                ISqlNode right = ParseAndExpression(context);
                left = new BinaryExpression { Left = left, Operator = opToken.Value.ToUpperInvariant(), Right = right };
            }
            return left;
        }


        /// <summary>
        /// Parses logical AND expressions.
        /// </summary>
        private ISqlNode ParseAndExpression(IParserContext context)
        {
            ISqlNode left = ParseComparisonExpression(context);

            while (context.IsKeyword("AND"))
            {
                var opToken = context.ConsumeToken("AND");
                ISqlNode right = ParseComparisonExpression(context);
                left = new BinaryExpression { Left = left, Operator = opToken.Value.ToUpperInvariant(), Right = right };
            }
            return left;
        }

        /// <summary>
        /// Parses a comparison expression (e.g., A = B, X > Y, col IS NULL, col IN (...)).
        /// </summary>
        private ISqlNode ParseComparisonExpression(IParserContext context)
        {
            ISqlNode left = ParseAdditiveExpression(context);

            if (context.PeekToken().Type == TokenType.Operator)
            {
                string op = context.PeekToken().Value;
                if (new[] { "=", "<", ">", "<=", ">=", "<>", "!=" }.Contains(op))
                {
                    context.ConsumeToken();
                    ISqlNode right = ParseAdditiveExpression(context);
                    return new BinaryExpression
                    {
                        Left = left,
                        Operator = op,
                        Right = right
                    };
                }
            }
            else if (context.IsKeyword("IS"))
            {
                context.ConsumeToken("IS");
                if (context.IsKeyword("NOT"))
                {
                    context.ConsumeToken("NOT");
                    context.ConsumeToken("NULL");
                    return new BinaryExpression
                    {
                        Left = left,
                        Operator = "IS NOT",
                        Right = new LiteralExpression { Value = "NULL", Type = LiteralType.Null }
                    };
                }
                else if (context.IsKeyword("NULL"))
                {
                    context.ConsumeToken("NULL");
                    return new BinaryExpression
                    {
                        Left = left,
                        Operator = "IS",
                        Right = new LiteralExpression { Value = "NULL", Type = LiteralType.Null }
                    };
                }
            }
            else if (context.IsKeyword("LIKE"))
            {
                context.ConsumeToken("LIKE");
                ISqlNode right = ParseAdditiveExpression(context);
                return new BinaryExpression
                {
                    Left = left,
                    Operator = "LIKE",
                    Right = right
                };
            }
            else if (context.IsKeyword("IN")) // Original IN block
            {
                left = ParseInExpression(context, left, false);
            }
            // THIS IS THE NEW PART FOR NOT IN
            else if (context.IsKeyword("NOT") && context.PeekToken(1).Type == TokenType.Keyword && context.PeekToken(1).Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
            {
                left = ParseInExpression(context, left, true);
            }

            return left;
        }

        /// <summary>
        /// Parses additive expressions (+, -).
        /// </summary>
        private ISqlNode ParseAdditiveExpression(IParserContext context)
        {
            ISqlNode left = ParseMultiplicativeExpression(context);

            while (context.PeekToken().Type == TokenType.Operator && (context.PeekToken().Value == "+" || context.PeekToken().Value == "-"))
            {
                var opToken = context.ConsumeToken();
                left = new BinaryExpression { Left = left, Operator = opToken.Value, Right = ParseMultiplicativeExpression(context) };
            }

            return left;
        }

        /// <summary>
        /// Parses multiplicative expressions (*, /).
        /// </summary>
        private ISqlNode ParseMultiplicativeExpression(IParserContext context)
        {
            ISqlNode left = ParseUnaryExpression(context);

            while (context.PeekToken().Type == TokenType.Operator && (context.PeekToken().Value == "*" || context.PeekToken().Value == "/"))
            {
                var opToken = context.ConsumeToken();
                left = new BinaryExpression { Left = left, Operator = opToken.Value, Right = ParseUnaryExpression(context) };
            }

            return left;
        }

        /// <summary>
        /// Parses unary expressions (NOT, -).
        /// </summary>
        private ISqlNode ParseUnaryExpression(IParserContext context)
        {
            if (context.IsKeyword("NOT"))
            {
                context.ConsumeToken("NOT");
                return new UnaryExpression { Operator = "NOT", Expression = ParseUnaryExpression(context) };
            }
            else if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "-")
            {
                context.ConsumeToken("-", TokenType.Operator);
                return new UnaryExpression { Operator = "-", Expression = ParsePrimaryExpression(context) };
            }
            else if (context.IsKeyword("EXISTS"))
            {
                context.ConsumeToken("EXISTS");
                context.ConsumeToken("(", TokenType.Parenthesis);
                var subqueryExpr = new SubqueryExpression { SubqueryStatement = _statementParserFactory.ParseStatement(context) }; // Use the factory here
                context.ConsumeToken(")", TokenType.Parenthesis);
                return new UnaryExpression { Operator = "EXISTS", Expression = subqueryExpr };
            }

            return ParsePrimaryExpression(context);
        }


        /// <summary>
        /// Parses a primary expression (leaf nodes in the expression tree).
        /// </summary>
        private ISqlNode ParsePrimaryExpression(IParserContext context)
        {
            var token = context.PeekToken();

            if (token.Type == TokenType.Parenthesis && token.Value == "(")
            {
                context.ConsumeToken("(", TokenType.Parenthesis);
                if (context.IsKeyword("SELECT"))
                {
                    var subquery = new SubqueryExpression { SubqueryStatement = _statementParserFactory.ParseStatement(context) }; // Use the factory here
                    context.ConsumeToken(")", TokenType.Parenthesis);
                    return subquery;
                }

                var expr = Parse(context); // Recursive call for nested expressions
                context.ConsumeToken(")", TokenType.Parenthesis);
                return expr;
            }

            if ((token.Type == TokenType.Identifier && context.PeekToken(1).Value == "(") ||
                (token.Type == TokenType.Keyword && IsKnownFunction(token.Value) && context.PeekToken(1).Value == "("))
            {
                return ParseFunctionCall(context);
            }

            if (token.Type == TokenType.Identifier)
            {
                var id1 = context.ConsumeToken();
                bool id1Quoted = id1.IsQuoted;

                if (context.PeekToken().Value == ".")
                {
                    context.ConsumeToken(".", TokenType.Operator);

                    var id2 = context.ConsumeToken(context.PeekToken().Value, TokenType.Identifier);
                    bool id2Quoted = id2.IsQuoted;

                    if (context.PeekToken().Value == ".")
                    {
                        context.ConsumeToken(".", TokenType.Operator);
                        var id3 = context.ConsumeToken(context.PeekToken().Value, TokenType.Identifier);
                        bool id3Quoted = id3.IsQuoted;

                        return new ColumnExpression
                        {
                            SchemaName = id1.Value,
                            TableAlias = id2.Value,
                            ColumnName = id3.Value,
                            SchemaNameQuoted = id1Quoted,
                            TableAliasQuoted = id2Quoted,
                            ColumnNameQuoted = id3Quoted
                        };
                    }

                    return new ColumnExpression
                    {
                        TableAlias = id1.Value,
                        ColumnName = id2.Value,
                        TableAliasQuoted = id1Quoted,
                        ColumnNameQuoted = id2Quoted
                    };
                }

                return new ColumnExpression
                {
                    ColumnName = id1.Value,
                    ColumnNameQuoted = id1Quoted
                };
            }

            if (token.Type == TokenType.StringLiteral)
            {
                var literalToken = context.ConsumeToken();
                return new LiteralExpression
                {
                    Value = literalToken.Value,
                    Type = LiteralType.String,
                    IsUnicode = literalToken.IsUnicodeString
                };
            }

            if (token.Type == TokenType.NumericLiteral)
                return new LiteralExpression { Value = context.ConsumeToken().Value, Type = LiteralType.Number };

            if (context.IsKeyword("NULL"))
            {
                context.ConsumeToken("NULL");
                return new LiteralExpression { Value = "NULL", Type = LiteralType.Null };
            }

            throw new SqlParseException($"Unexpected token in primary expression: '{token.Value}'", token.StartIndex);
        }

        private FunctionCallExpression ParseFunctionCall(IParserContext context)
        {
            var functionNameToken = context.ConsumeToken();
            if (functionNameToken.Type != TokenType.Identifier && functionNameToken.Type != TokenType.Keyword)
            {
                throw new SqlParseException($"Expected function name (Identifier or Keyword), got '{functionNameToken.Value}' ({functionNameToken.Type})", functionNameToken.StartIndex);
            }
            var functionName = functionNameToken.Value;

            context.ConsumeToken("(", TokenType.Parenthesis);

            var funcCall = new FunctionCallExpression { FunctionName = functionName };

            if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "*")
            {
                funcCall.IsAllColumns = true;
                context.ConsumeToken("*", TokenType.Operator);
            }
            else
            {
                if (context.PeekToken().Type != TokenType.Parenthesis || context.PeekToken().Value != ")")
                {
                    do
                    {
                        funcCall.Arguments.Add(Parse(context)); // Arguments can be expressions
                    } while (context.MatchToken(",", TokenType.Comma));
                }
            }
            context.ConsumeToken(")", TokenType.Parenthesis);
            return funcCall;
        }

        private static bool IsKnownFunction(string keyword)
        {
            return keyword.Equals("COUNT", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("SUM", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("AVG", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("MIN", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("MAX", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("SUBSTRING", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("GETDATE", StringComparison.OrdinalIgnoreCase);
        }

        private InExpression ParseInExpression(IParserContext context, ISqlNode left, bool isNegated)
        {
            if (isNegated)
            {
                context.ConsumeToken("NOT");
            }

            context.ConsumeToken("IN");
            context.ConsumeToken("(", TokenType.Parenthesis);

            var inExpression = new InExpression
            {
                Expression = left,
                IsNegated = isNegated
            };

            if (context.IsKeyword("SELECT") ||
                (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken(1).Type == TokenType.Keyword && context.PeekToken(1).Value.Equals("SELECT", StringComparison.OrdinalIgnoreCase)))
            {
                inExpression.Subquery = _statementParserFactory.ParseStatement(context);
            }
            else
            {
                do
                {
                    inExpression.Values.Add(Parse(context));
                } while (context.MatchToken(",", TokenType.Comma));
            }

            context.ConsumeToken(")", TokenType.Parenthesis);
            return inExpression;
        }
    }
}
