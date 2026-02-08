using System;
using System.Collections.Generic;
using System.Linq;
using SqlForge.Parsers;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{
    /// <summary>
    /// Implements ISqlParser for SQL Anywhere dialect.
    /// Orchestrates the parsing process using specialized parser components and a factory.
    /// </summary>
    public class SqlAnywhereParser : BaseSqlParser
    {
        private readonly IStatementParserFactory _statementParserFactory;

        public SqlAnywhereParser(IStatementParserFactory statementParserFactory)
        {
            _statementParserFactory = statementParserFactory;
        }

        public override SqlStatement Parse(string sql, SqlDialect dialect = SqlDialect.SqlAnywhere)
        {
            var tokenizer = new Tokenizer(sql);
            var tokens = tokenizer.Tokenize();

            // Corrected condition: throw exception only if the token list is truly empty,
            // or if it contains only the EOF token (meaning the SQL string was empty/whitespace).
            if (tokens == null || tokens.Count == 0 || (tokens.Count == 1 && tokens.LastOrDefault()?.Type == TokenType.EOF))
            {
                throw new SqlParseException("Empty or invalid SQL string.");
            }

            var context = new ParserContext(tokens);

            var statement = ParseTopLevelStatement(context);

            return statement;
        }

        /// <summary>
        /// Parses an entire SQL statement including optional semicolon and ensures it ends properly.
        /// </summary>
        private SqlStatement ParseTopLevelStatement(IParserContext context)
        {
            var statement = _statementParserFactory.ParseStatement(context);

            if (context.PeekToken().Type == TokenType.Semicolon)
                context.ConsumeToken(";", TokenType.Semicolon);

            while (context.MatchToken(";", TokenType.Semicolon)) { }

            if (context.PeekToken().Type != TokenType.EOF)
            {
                var next = context.PeekToken();
                throw new SqlParseException($"Unexpected token '{next.Value}' after statement completion.", next.StartIndex);
            }

            return statement;
        }
    }
}