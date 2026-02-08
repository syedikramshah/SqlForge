using System;
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

        public SqlAnywhereStatementParserFactory(SelectStatementParser selectStatementParser)
        {
            _selectStatementParser = selectStatementParser;
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
                    context.ConsumeToken("ALL");
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

            return new SqlStatement
            {
                Type = StatementType.Select,
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
            else if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
            {
                context.ConsumeToken("(", TokenType.Parenthesis);

                // Inside parentheses, we allow full set operator handling.
                // This enables explicit grouping like (A UNION B) EXCEPT C.
                // The parentheses define a self-contained unit that can include set operators.
                var body = ParseStatement(context).Body;

                context.ConsumeToken(")", TokenType.Parenthesis);
                return body;
            }
            else
            {
                throw new SqlParseException($"Unsupported statement type: '{context.CurrentToken().Value}'", context.CurrentToken().StartIndex);
            }
        }

        public IStatementParser GetParserForStatementType(IParserContext context)
        {
            if (_selectStatementParser.CanParse(context))
            {
                return _selectStatementParser;
            }
            return null;
        }
    }
}
