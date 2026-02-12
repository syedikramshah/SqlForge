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
    /// Parses SQL expressions for the MySQL dialect.
    /// </summary>
    public class MySqlExpressionParser : IExpressionParser
    {
        private readonly IStatementParserFactory _statementParserFactory;

        public MySqlExpressionParser(IStatementParserFactory statementParserFactory)
        {
            _statementParserFactory = statementParserFactory;
        }

        public ISqlNode Parse(IParserContext context)
        {
            return ParseOrExpression(context);
        }

        private ISqlNode ParseOrExpression(IParserContext context)
        {
            ISqlNode left = ParseXorExpression(context);

            while (context.IsKeyword("OR") || IsOperator(context.PeekToken(), "||"))
            {
                var opToken = context.ConsumeToken();
                string op = opToken.Type == TokenType.Keyword ? opToken.Value.ToUpperInvariant() : opToken.Value;
                ISqlNode right = ParseXorExpression(context);
                left = new BinaryExpression { Left = left, Operator = op, Right = right };
            }

            return left;
        }

        private ISqlNode ParseXorExpression(IParserContext context)
        {
            ISqlNode left = ParseAndExpression(context);

            while (context.IsKeyword("XOR"))
            {
                var opToken = context.ConsumeToken("XOR");
                ISqlNode right = ParseAndExpression(context);
                left = new BinaryExpression { Left = left, Operator = opToken.Value.ToUpperInvariant(), Right = right };
            }

            return left;
        }

        private ISqlNode ParseAndExpression(IParserContext context)
        {
            ISqlNode left = ParseComparisonExpression(context);

            while (context.IsKeyword("AND") || IsOperator(context.PeekToken(), "&&"))
            {
                var opToken = context.ConsumeToken();
                string op = opToken.Type == TokenType.Keyword ? opToken.Value.ToUpperInvariant() : opToken.Value;
                ISqlNode right = ParseComparisonExpression(context);
                left = new BinaryExpression { Left = left, Operator = op, Right = right };
            }

            return left;
        }

        private ISqlNode ParseComparisonExpression(IParserContext context)
        {
            ISqlNode left = ParseBitwiseOrExpression(context);

            if (context.IsKeyword("NOT") && context.PeekToken(1).Type == TokenType.Keyword &&
                context.PeekToken(1).Value.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase))
            {
                context.ConsumeToken("NOT");
                context.ConsumeToken("BETWEEN");
                var lower = ParseBitwiseOrExpression(context);
                context.ConsumeToken("AND");
                var upper = ParseBitwiseOrExpression(context);
                return new BetweenExpression { Expression = left, Lower = lower, Upper = upper, IsNegated = true };
            }

            if (context.IsKeyword("BETWEEN"))
            {
                context.ConsumeToken("BETWEEN");
                var lower = ParseBitwiseOrExpression(context);
                context.ConsumeToken("AND");
                var upper = ParseBitwiseOrExpression(context);
                return new BetweenExpression { Expression = left, Lower = lower, Upper = upper, IsNegated = false };
            }

            if (context.IsKeyword("NOT") && context.PeekToken(1).Type == TokenType.Keyword &&
                (context.PeekToken(1).Value.Equals("LIKE", StringComparison.OrdinalIgnoreCase) ||
                 context.PeekToken(1).Value.Equals("REGEXP", StringComparison.OrdinalIgnoreCase) ||
                 context.PeekToken(1).Value.Equals("RLIKE", StringComparison.OrdinalIgnoreCase)))
            {
                context.ConsumeToken("NOT");
                var opToken = context.ConsumeToken();
                return new BinaryExpression { Left = left, Operator = "NOT " + opToken.Value.ToUpperInvariant(), Right = ParseBitwiseOrExpression(context) };
            }

            if (context.IsKeyword("LIKE") || context.IsKeyword("REGEXP") || context.IsKeyword("RLIKE"))
            {
                var opToken = context.ConsumeToken();
                return new BinaryExpression { Left = left, Operator = opToken.Value.ToUpperInvariant(), Right = ParseBitwiseOrExpression(context) };
            }

            if (context.IsKeyword("IS"))
            {
                context.ConsumeToken("IS");
                bool isNot = false;
                if (context.IsKeyword("NOT"))
                {
                    context.ConsumeToken("NOT");
                    isNot = true;
                }

                if (context.IsKeyword("NULL"))
                {
                    context.ConsumeToken("NULL");
                    return new BinaryExpression
                    {
                        Left = left,
                        Operator = isNot ? "IS NOT" : "IS",
                        Right = new LiteralExpression { Value = "NULL", Type = LiteralType.Null }
                    };
                }

                if (context.IsKeyword("TRUE") || context.IsKeyword("FALSE") || context.IsKeyword("UNKNOWN"))
                {
                    var literalToken = context.ConsumeToken();
                    var literalType = literalToken.Value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                                     literalToken.Value.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
                        ? LiteralType.Boolean
                        : LiteralType.Null;

                    return new BinaryExpression
                    {
                        Left = left,
                        Operator = isNot ? "IS NOT" : "IS",
                        Right = new LiteralExpression { Value = literalToken.Value.ToUpperInvariant(), Type = literalType }
                    };
                }
            }

            if (context.PeekToken().Type == TokenType.Operator)
            {
                string op = context.PeekToken().Value;
                if (new[] { "=", "<", ">", "<=", ">=", "<>", "!=", "<=>" }.Contains(op))
                {
                    context.ConsumeToken();
                    ISqlNode right = ParseBitwiseOrExpression(context);
                    return new BinaryExpression { Left = left, Operator = op, Right = right };
                }
            }

            if (context.IsKeyword("IN"))
            {
                return ParseInExpression(context, left, false);
            }

            if (context.IsKeyword("NOT") && context.PeekToken(1).Type == TokenType.Keyword &&
                context.PeekToken(1).Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
            {
                return ParseInExpression(context, left, true);
            }

            return left;
        }

        private ISqlNode ParseBitwiseOrExpression(IParserContext context)
        {
            ISqlNode left = ParseBitwiseXorExpression(context);

            while (IsOperator(context.PeekToken(), "|"))
            {
                var opToken = context.ConsumeToken();
                ISqlNode right = ParseBitwiseXorExpression(context);
                left = new BinaryExpression { Left = left, Operator = opToken.Value, Right = right };
            }

            return left;
        }

        private ISqlNode ParseBitwiseXorExpression(IParserContext context)
        {
            ISqlNode left = ParseBitwiseAndExpression(context);

            while (IsOperator(context.PeekToken(), "^"))
            {
                var opToken = context.ConsumeToken();
                ISqlNode right = ParseBitwiseAndExpression(context);
                left = new BinaryExpression { Left = left, Operator = opToken.Value, Right = right };
            }

            return left;
        }

        private ISqlNode ParseBitwiseAndExpression(IParserContext context)
        {
            ISqlNode left = ParseShiftExpression(context);

            while (IsOperator(context.PeekToken(), "&"))
            {
                var opToken = context.ConsumeToken();
                ISqlNode right = ParseShiftExpression(context);
                left = new BinaryExpression { Left = left, Operator = opToken.Value, Right = right };
            }

            return left;
        }

        private ISqlNode ParseShiftExpression(IParserContext context)
        {
            ISqlNode left = ParseAdditiveExpression(context);

            while (IsOperator(context.PeekToken(), "<<") || IsOperator(context.PeekToken(), ">>"))
            {
                var opToken = context.ConsumeToken();
                ISqlNode right = ParseAdditiveExpression(context);
                left = new BinaryExpression { Left = left, Operator = opToken.Value, Right = right };
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

            while (context.PeekToken().Type == TokenType.Operator &&
                   (context.PeekToken().Value == "*" || context.PeekToken().Value == "/" || context.PeekToken().Value == "%") ||
                   context.IsKeyword("DIV") || context.IsKeyword("MOD"))
            {
                var opToken = context.ConsumeToken();
                string op = opToken.Type == TokenType.Keyword ? opToken.Value.ToUpperInvariant() : opToken.Value;
                left = new BinaryExpression { Left = left, Operator = op, Right = ParseUnaryExpression(context) };
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

            if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "!")
            {
                context.ConsumeToken("!", TokenType.Operator);
                return new UnaryExpression { Operator = "NOT", Expression = ParseUnaryExpression(context) };
            }

            if (context.PeekToken().Type == TokenType.Operator &&
                (context.PeekToken().Value == "-" || context.PeekToken().Value == "+"))
            {
                var opToken = context.ConsumeToken();
                return new UnaryExpression { Operator = opToken.Value, Expression = ParsePrimaryExpression(context) };
            }

            if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "~")
            {
                context.ConsumeToken("~", TokenType.Operator);
                return new UnaryExpression { Operator = "~", Expression = ParsePrimaryExpression(context) };
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

            if (context.IsKeyword("TRUE") || context.IsKeyword("FALSE") || context.IsKeyword("UNKNOWN"))
            {
                var literalToken = context.ConsumeToken();
                var literalType = literalToken.Value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                                 literalToken.Value.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
                    ? LiteralType.Boolean
                    : LiteralType.Null;
                return new LiteralExpression { Value = literalToken.Value.ToUpperInvariant(), Type = literalType };
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

        private FunctionCallExpression ParseFunctionCall(IParserContext context)
        {
            var token = context.ConsumeToken();
            var function = new FunctionCallExpression { FunctionName = token.Value.ToUpperInvariant() };

            context.ConsumeToken("(", TokenType.Parenthesis);

            if (context.PeekToken().Type == TokenType.Operator && context.PeekToken().Value == "*")
            {
                context.ConsumeToken("*", TokenType.Operator);
                function.IsAllColumns = true;
                context.ConsumeToken(")", TokenType.Parenthesis);
                return function;
            }

            if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == ")")
            {
                context.ConsumeToken(")", TokenType.Parenthesis);
                return function;
            }

            do
            {
                function.Arguments.Add(Parse(context));
            } while (context.MatchToken(",", TokenType.Comma));

            context.ConsumeToken(")", TokenType.Parenthesis);
            return function;
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
                throw new SqlParseException("Expected ROWS, RANGE, or GROUPS in window frame", context.PeekToken().StartIndex);
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

                if (context.IsKeyword("FOLLOWING"))
                {
                    context.ConsumeToken("FOLLOWING");
                    return new WindowFrameBound { Type = WindowFrameBoundType.UnboundedFollowing };
                }

                throw new SqlParseException("Expected PRECEDING or FOLLOWING after UNBOUNDED", context.PeekToken().StartIndex);
            }

            if (context.IsKeyword("CURRENT"))
            {
                context.ConsumeToken("CURRENT");
                context.ConsumeToken("ROW");
                return new WindowFrameBound { Type = WindowFrameBoundType.CurrentRow };
            }

            var expr = Parse(context);
            if (context.IsKeyword("PRECEDING"))
            {
                context.ConsumeToken("PRECEDING");
                return new WindowFrameBound { Type = WindowFrameBoundType.Preceding, OffsetExpression = expr };
            }

            if (context.IsKeyword("FOLLOWING"))
            {
                context.ConsumeToken("FOLLOWING");
                return new WindowFrameBound { Type = WindowFrameBoundType.Following, OffsetExpression = expr };
            }

            throw new SqlParseException("Expected PRECEDING or FOLLOWING in window frame bounds", context.PeekToken().StartIndex);
        }

        private ISqlNode ParseInExpression(IParserContext context, ISqlNode left, bool isNegated)
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
                (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken(1).Type == TokenType.Keyword &&
                 context.PeekToken(1).Value.Equals("SELECT", StringComparison.OrdinalIgnoreCase)))
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

        private bool IsKnownFunction(string name)
        {
            return new[]
            {
                "COUNT", "SUM", "AVG", "MIN", "MAX",
                "SUBSTRING", "GETDATE",
                "ROW_NUMBER", "RANK", "DENSE_RANK", "NTILE",
                "LAG", "LEAD", "FIRST_VALUE", "LAST_VALUE"
            }.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsOperator(Token token, string op)
        {
            return token.Type == TokenType.Operator && token.Value == op;
        }
    }
}
