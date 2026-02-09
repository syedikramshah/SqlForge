using System;
using System.Collections.Generic;
using System.Linq;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;

namespace SqlForge.Parsers
{
    /// <summary>
    /// Parses SQL expressions for the MS SQL Server dialect.
    /// </summary>
    public class MsSqlExpressionParser : IExpressionParser
    {
        private readonly IStatementParserFactory _statementParserFactory;

        public MsSqlExpressionParser(IStatementParserFactory statementParserFactory)
        {
            _statementParserFactory = statementParserFactory;
        }

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
                    return new BinaryExpression { Left = left, Operator = op, Right = right };
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

                if (context.IsKeyword("NULL"))
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
                return new BinaryExpression { Left = left, Operator = "LIKE", Right = ParseAdditiveExpression(context) };
            }
            else if (context.IsKeyword("IN"))
            {
                context.ConsumeToken("IN");
                context.ConsumeToken("(", TokenType.Parenthesis);

                ISqlNode inRight;
                if (context.IsKeyword("SELECT") ||
                    (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken(1).Type == TokenType.Keyword && context.PeekToken(1).Value.Equals("SELECT", StringComparison.OrdinalIgnoreCase)))
                {
                    inRight = new SubqueryExpression { SubqueryStatement = _statementParserFactory.ParseStatement(context) };
                }
                else
                {
                    var inArguments = new List<ISqlNode>();
                    do
                    {
                        inArguments.Add(Parse(context));
                    } while (context.MatchToken(",", TokenType.Comma));
                    inRight = new FunctionCallExpression { FunctionName = "IN_LIST", Arguments = inArguments };
                }

                context.ConsumeToken(")", TokenType.Parenthesis);
                left = new BinaryExpression { Left = left, Operator = "IN", Right = inRight };
            }
            else if (context.IsKeyword("NOT") && context.PeekToken(1).Type == TokenType.Keyword && context.PeekToken(1).Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
            {
                context.ConsumeToken("NOT");
                context.ConsumeToken("IN");
                context.ConsumeToken("(", TokenType.Parenthesis);

                ISqlNode inRight;
                if (context.IsKeyword("SELECT") ||
                    (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken(1).Type == TokenType.Keyword && context.PeekToken(1).Value.Equals("SELECT", StringComparison.OrdinalIgnoreCase)))
                {
                    inRight = new SubqueryExpression { SubqueryStatement = _statementParserFactory.ParseStatement(context) };
                }
                else
                {
                    var inArguments = new List<ISqlNode>();
                    do
                    {
                        inArguments.Add(Parse(context));
                    } while (context.MatchToken(",", TokenType.Comma));
                    inRight = new FunctionCallExpression { FunctionName = "IN_LIST", Arguments = inArguments };
                }

                context.ConsumeToken(")", TokenType.Parenthesis);
                var inExpression = new BinaryExpression { Left = left, Operator = "IN", Right = inRight };
                left = new UnaryExpression { Operator = "NOT", Expression = inExpression };
            }

            return left;
        }

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

        private ISqlNode ParseUnaryExpression(IParserContext context)
        {
            if (context.IsKeyword("NOT"))
            {
                context.ConsumeToken("NOT");
                return new UnaryExpression { Operator = "NOT", Expression = ParseUnaryExpression(context) };
            }

            if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "-")
            {
                context.ConsumeToken("-", TokenType.Operator);
                return new UnaryExpression { Operator = "-", Expression = ParsePrimaryExpression(context) };
            }

            if (context.IsKeyword("EXISTS"))
            {
                context.ConsumeToken("EXISTS");
                context.ConsumeToken("(", TokenType.Parenthesis);
                var subqueryExpr = new SubqueryExpression { SubqueryStatement = _statementParserFactory.ParseStatement(context) };
                context.ConsumeToken(")", TokenType.Parenthesis);
                return new UnaryExpression { Operator = "EXISTS", Expression = subqueryExpr };
            }

            return ParsePrimaryExpression(context);
        }

        private ISqlNode ParsePrimaryExpression(IParserContext context)
        {
            var token = context.PeekToken();

            if (token.Type == TokenType.Parenthesis && token.Value == "(")
            {
                context.ConsumeToken("(", TokenType.Parenthesis);
                if (context.IsKeyword("SELECT"))
                {
                    var subquery = new SubqueryExpression { SubqueryStatement = _statementParserFactory.ParseStatement(context) };
                    context.ConsumeToken(")", TokenType.Parenthesis);
                    return subquery;
                }

                var expr = Parse(context);
                context.ConsumeToken(")", TokenType.Parenthesis);
                return expr;
            }

            if ((token.Type == TokenType.Identifier && context.PeekToken(1).Value == "(") ||
                (token.Type == TokenType.Keyword && IsKnownFunction(token.Value) && context.PeekToken(1).Value == "("))
            {
                return ParseFunctionCallOrWindowFunction(context);
            }

            if (token.Type == TokenType.Identifier)
            {
                var id1 = context.ConsumeToken();

                if (context.PeekToken().Value == ".")
                {
                    context.ConsumeToken(".", TokenType.Operator);
                    var id2 = context.ConsumeToken(context.PeekToken().Value, TokenType.Identifier);

                    if (context.PeekToken().Value == ".")
                    {
                        context.ConsumeToken(".", TokenType.Operator);
                        var id3 = context.ConsumeToken(context.PeekToken().Value, TokenType.Identifier);
                        return new ColumnExpression
                        {
                            SchemaName = id1.Value,
                            TableAlias = id2.Value,
                            ColumnName = id3.Value,
                            SchemaNameQuoted = id1.IsQuoted,
                            TableAliasQuoted = id2.IsQuoted,
                            ColumnNameQuoted = id3.IsQuoted,
                            SchemaQuoteStyle = id1.QuoteStyle,
                            TableAliasQuoteStyle = id2.QuoteStyle,
                            ColumnQuoteStyle = id3.QuoteStyle
                        };
                    }

                    return new ColumnExpression
                    {
                        TableAlias = id1.Value,
                        ColumnName = id2.Value,
                        TableAliasQuoted = id1.IsQuoted,
                        ColumnNameQuoted = id2.IsQuoted,
                        TableAliasQuoteStyle = id1.QuoteStyle,
                        ColumnQuoteStyle = id2.QuoteStyle
                    };
                }

                return new ColumnExpression
                {
                    ColumnName = id1.Value,
                    ColumnNameQuoted = id1.IsQuoted,
                    ColumnQuoteStyle = id1.QuoteStyle
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
            {
                return new LiteralExpression { Value = context.ConsumeToken().Value, Type = LiteralType.Number };
            }

            if (context.IsKeyword("NULL"))
            {
                context.ConsumeToken("NULL");
                return new LiteralExpression { Value = "NULL", Type = LiteralType.Null };
            }

            throw new SqlParseException($"Unexpected token in primary expression: '{token.Value}'", token.StartIndex);
        }

        private ISqlNode ParseFunctionCallOrWindowFunction(IParserContext context)
        {
            var functionCall = ParseFunctionCall(context);
            if (!context.IsKeyword("OVER"))
            {
                return functionCall;
            }

            context.ConsumeToken("OVER");
            context.ConsumeToken("(", TokenType.Parenthesis);

            var window = new WindowFunctionExpression
            {
                FunctionName = functionCall.FunctionName,
                Arguments = functionCall.Arguments
            };

            if (context.IsKeyword("PARTITION"))
            {
                context.ConsumeToken("PARTITION");
                context.ConsumeToken("BY");
                do
                {
                    window.PartitionByExpressions.Add(Parse(context));
                } while (context.MatchToken(",", TokenType.Comma));
            }

            if (context.IsKeyword("ORDER"))
            {
                context.ConsumeToken("ORDER");
                context.ConsumeToken("BY");
                do
                {
                    var item = new OrderItem { Expression = Parse(context), IsAscending = true };
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

                    window.OrderByItems.Add(item);
                } while (context.MatchToken(",", TokenType.Comma));
            }

            if (context.IsKeyword("ROWS") || context.IsKeyword("RANGE") || context.IsKeyword("GROUPS"))
            {
                window.Frame = ParseWindowFrame(context);
            }

            context.ConsumeToken(")", TokenType.Parenthesis);
            return window;
        }

        private WindowFrame ParseWindowFrame(IParserContext context)
        {
            var frame = new WindowFrame();

            if (context.IsKeyword("ROWS"))
            {
                context.ConsumeToken("ROWS");
                frame.Type = WindowFrameType.Rows;
            }
            else if (context.IsKeyword("RANGE"))
            {
                context.ConsumeToken("RANGE");
                frame.Type = WindowFrameType.Range;
            }
            else if (context.IsKeyword("GROUPS"))
            {
                context.ConsumeToken("GROUPS");
                frame.Type = WindowFrameType.Groups;
            }
            else
            {
                throw new SqlParseException("Expected ROWS, RANGE, or GROUPS in window frame specification", context.PeekToken().StartIndex);
            }

            if (context.IsKeyword("BETWEEN"))
            {
                context.ConsumeToken("BETWEEN");
                frame.StartBound = ParseWindowFrameBound(context);
                context.ConsumeToken("AND");
                frame.EndBound = ParseWindowFrameBound(context);
            }
            else
            {
                frame.StartBound = ParseWindowFrameBound(context);
                frame.EndBound = new WindowFrameBound { Type = WindowFrameBoundType.CurrentRow };
            }

            return frame;
        }

        private WindowFrameBound ParseWindowFrameBound(IParserContext context)
        {
            if (context.IsKeyword("UNBOUNDED"))
            {
                context.ConsumeToken("UNBOUNDED");
                if (context.IsKeyword("PRECEDING"))
                {
                    context.ConsumeToken("PRECEDING");
                    return new WindowFrameBound { Type = WindowFrameBoundType.UnboundedPreceding };
                }

                context.ConsumeToken("FOLLOWING");
                return new WindowFrameBound { Type = WindowFrameBoundType.UnboundedFollowing };
            }

            if (context.IsKeyword("CURRENT"))
            {
                context.ConsumeToken("CURRENT");
                context.ConsumeToken("ROW");
                return new WindowFrameBound { Type = WindowFrameBoundType.CurrentRow };
            }

            var offsetExpr = Parse(context);
            if (context.IsKeyword("PRECEDING"))
            {
                context.ConsumeToken("PRECEDING");
                return new WindowFrameBound { Type = WindowFrameBoundType.Preceding, OffsetExpression = offsetExpr };
            }

            context.ConsumeToken("FOLLOWING");
            return new WindowFrameBound { Type = WindowFrameBoundType.Following, OffsetExpression = offsetExpr };
        }

        private FunctionCallExpression ParseFunctionCall(IParserContext context)
        {
            var functionNameToken = context.ConsumeToken();
            if (functionNameToken.Type != TokenType.Identifier && functionNameToken.Type != TokenType.Keyword)
            {
                throw new SqlParseException($"Expected function name (Identifier or Keyword), got '{functionNameToken.Value}' ({functionNameToken.Type})", functionNameToken.StartIndex);
            }

            context.ConsumeToken("(", TokenType.Parenthesis);
            var funcCall = new FunctionCallExpression { FunctionName = functionNameToken.Value };

            if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "*")
            {
                funcCall.IsAllColumns = true;
                context.ConsumeToken("*", TokenType.Operator);
            }
            else if (context.PeekToken().Type != TokenType.Parenthesis || context.PeekToken().Value != ")")
            {
                do
                {
                    funcCall.Arguments.Add(Parse(context));
                } while (context.MatchToken(",", TokenType.Comma));
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
                   keyword.Equals("GETDATE", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("ROW_NUMBER", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("RANK", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("DENSE_RANK", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("NTILE", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("LAG", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("LEAD", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("FIRST_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   keyword.Equals("LAST_VALUE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
