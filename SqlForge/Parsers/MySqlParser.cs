using System.Linq;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{
    /// <summary>
    /// Implements ISqlParser for MySQL dialect.
    /// </summary>
    public class MySqlParser : BaseSqlParser
    {
        private readonly IStatementParserFactory _statementParserFactory;

        public MySqlParser(IStatementParserFactory statementParserFactory)
        {
            _statementParserFactory = statementParserFactory;
        }

        public override SqlStatement Parse(string sql, SqlDialect dialect = SqlDialect.MySql)
        {
            var effectiveDialect = dialect == SqlDialect.Generic ? SqlDialect.MySql : dialect;

            var tokenizer = new Tokenizer(sql, effectiveDialect);
            var tokens = tokenizer.Tokenize();

            if (tokens == null || tokens.Count == 0 || (tokens.Count == 1 && tokens.LastOrDefault()?.Type == TokenType.EOF))
            {
                throw new SqlParseException("Empty or invalid SQL string.");
            }

            var context = new ParserContext(tokens, effectiveDialect);
            return ParseTopLevelStatement(context);
        }

        private SqlStatement ParseTopLevelStatement(IParserContext context)
        {
            WithClause withClause = null;
            if (context.IsKeyword("WITH"))
            {
                withClause = ParseWithClause(context);
            }

            var statement = _statementParserFactory.ParseStatement(context);
            statement.WithClause = withClause;

            if (context.PeekToken().Type == TokenType.Semicolon)
            {
                context.ConsumeToken(";", TokenType.Semicolon);
            }

            while (context.MatchToken(";", TokenType.Semicolon))
            {
            }

            if (context.PeekToken().Type != TokenType.EOF)
            {
                var next = context.PeekToken();
                throw new SqlParseException($"Unexpected token '{next.Value}' after statement completion.", next.StartIndex);
            }

            return statement;
        }

        private WithClause ParseWithClause(IParserContext context)
        {
            context.ConsumeToken("WITH");
            var withClause = new WithClause();

            do
            {
                var cte = new CommonTableExpression();
                var nameToken = context.ConsumeToken();
                if (nameToken.Type != TokenType.Identifier && nameToken.Type != TokenType.Keyword)
                {
                    throw new SqlParseException("Expected CTE name.", nameToken.StartIndex);
                }

                cte.Name = nameToken.Value;

                if (context.PeekToken().Type == TokenType.Parenthesis && context.PeekToken().Value == "(")
                {
                    context.ConsumeToken("(", TokenType.Parenthesis);
                    do
                    {
                        var col = context.ConsumeToken();
                        if (col.Type != TokenType.Identifier && col.Type != TokenType.Keyword)
                        {
                            throw new SqlParseException("Expected CTE column name.", col.StartIndex);
                        }

                        cte.ColumnNames.Add(col.Value);
                    } while (context.MatchToken(",", TokenType.Comma));

                    context.ConsumeToken(")", TokenType.Parenthesis);
                }

                context.ConsumeToken("AS");
                context.ConsumeToken("(", TokenType.Parenthesis);
                cte.Query = _statementParserFactory.ParseStatement(context);
                context.ConsumeToken(")", TokenType.Parenthesis);

                withClause.CommonTableExpressions.Add(cte);
            } while (context.MatchToken(",", TokenType.Comma));

            return withClause;
        }
    }
}
