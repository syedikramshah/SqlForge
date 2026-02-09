using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{
    public class MsSqlServerStatementParserFactory : IStatementParserFactory
    {
        private readonly MsSqlSelectStatementParser _selectStatementParser;

        public MsSqlServerStatementParserFactory(MsSqlSelectStatementParser selectStatementParser)
        {
            _selectStatementParser = selectStatementParser;
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
                {
                    opType = SetOperatorType.Except;
                }
                else if (upper == "UNION")
                {
                    opType = SetOperatorType.Union;
                }
                else if (upper == "INTERSECT")
                {
                    opType = SetOperatorType.Intersect;
                }
                else
                {
                    throw new SqlParseException("Unsupported set operator: " + operatorToken.Value, operatorToken.StartIndex);
                }

                if (context.IsKeyword("ALL"))
                {
                    if (opType == SetOperatorType.Union)
                    {
                        context.ConsumeToken("ALL");
                        opType = SetOperatorType.UnionAll;
                    }
                    else
                    {
                        throw new SqlParseException($"{upper} ALL is not supported in MS SQL Server.", context.PeekToken().StartIndex);
                    }
                }

                ISqlNode right = ParseSingleStatementBody(context);

                statementBody = new SetOperatorExpression
                {
                    Operator = opType,
                    Left = statementBody,
                    Right = right
                };
            }

            return new SqlStatement
            {
                Type = StatementType.Select,
                Body = statementBody
            };
        }

        private ISqlNode ParseSingleStatementBody(IParserContext context)
        {
            if (_selectStatementParser.CanParse(context))
            {
                return _selectStatementParser.Parse(context).Body;
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
    }
}
