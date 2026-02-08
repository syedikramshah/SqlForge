using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Parsers
{
    public class ParserContext : IParserContext
    {
        private readonly List<Token> _tokens;
        private int _currentTokenIndex;

        public ParserContext(List<Token> tokens)
        {
            _tokens = tokens;
            _currentTokenIndex = 0;
        }

        /// <summary>
        /// Peeks at the token at the current position + offset without consuming the token.
        /// </summary>
        public Token PeekToken(int offset = 0)
        {
            if (_currentTokenIndex + offset < _tokens.Count)
                return _tokens[_currentTokenIndex + offset];
            return new Token(TokenType.EOF, string.Empty);
        }

        /// <summary>
        /// Gets the current token without consuming it.
        /// </summary>
        public Token CurrentToken() => PeekToken(0); // Implemented by calling PeekToken(0)

        /// <summary>
        /// Consumes and returns the current token, advancing the index.
        /// </summary>
        public Token ConsumeToken()
        {
            if (_currentTokenIndex < _tokens.Count)
                return _tokens[_currentTokenIndex++];
            else throw new SqlParseException("Unexpected end of input.", _tokens.LastOrDefault()?.StartIndex ?? 0);
        }

        /// <summary>
        /// Consumes the current token if its value matches the expected value (case-insensitive for keywords).
        /// Throws SqlParseException if it doesn't match.
        /// </summary>
        public Token ConsumeToken(string expectedValue, TokenType expectedType = TokenType.Keyword) // Implemented
        {
            var token = ConsumeToken();
            bool isMatch = expectedType == TokenType.Keyword
                ? token.Value.Equals(expectedValue, StringComparison.OrdinalIgnoreCase)
                : token.Value.Equals(expectedValue, StringComparison.Ordinal);

            if (!isMatch || token.Type != expectedType)
            {
                throw new SqlParseException($"Expected '{expectedValue}' ({expectedType}), got '{token.Value}' ({token.Type})", token.StartIndex);
            }
            return token;
        }

        /// <summary>
        /// Checks if the current token matches the given value (case-insensitive for keywords) and type.
        /// If it matches, consumes the token and returns true. Otherwise, returns false and does not consume.
        /// </summary>
        public bool MatchToken(string value, TokenType type = TokenType.Keyword)
        {
            if (PeekToken().Type == type)
            {
                bool isMatch = type == TokenType.Keyword
                    ? PeekToken().Value.Equals(value, StringComparison.OrdinalIgnoreCase)
                    : PeekToken().Value.Equals(value, StringComparison.Ordinal);

                if (isMatch)
                {
                    ConsumeToken();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the current token is a specific keyword.
        /// </summary>
        public bool IsKeyword(string keyword)
        {
            var token = PeekToken();
            return token.Type == TokenType.Keyword && token.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);
        }
    }
}