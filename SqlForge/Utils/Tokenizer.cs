using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlForge.Enums;
using SqlForge.Exceptions;

namespace SqlForge.Utils
{
    /// <summary>
    /// A simple tokenizer (lexer) to convert a SQL string into a sequence of tokens.
    /// </summary>
    public class Tokenizer
    {
        private readonly string _sql;
        private int _position;

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "AND", "OR", "GROUP", "BY", "HAVING", "ORDER", "AS",
            "INSERT", "UPDATE", "DELETE", "CREATE", "TABLE", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "ON",
            "DISTINCT", "TOP", "UNION", "ALL", "EXCEPT", "INTERSECT", "IN", "NOT", "NULL", "IS",
            "COUNT", "SUM", "AVG", "MIN", "MAX", "SUBSTRING", "GETDATE",
            "CASE", "WHEN", "THEN", "ELSE", "END", "EXISTS", "OUTER",
            "LIKE", "ASC", "DESC",
            "WITH", "OVER", "PARTITION", "OFFSET", "FETCH", "ROWS", "ROW", "ONLY",
            "APPLY", "CROSS", "PERCENT", "TIES", "RANGE", "GROUPS", "NEXT", "FIRST",
            "BETWEEN", "FOLLOWING", "PRECEDING", "UNBOUNDED", "CURRENT"
        };

        private static readonly Dictionary<string, TokenType> Operators = new Dictionary<string, TokenType>(StringComparer.Ordinal)
        {
            {"=", TokenType.Operator}, {"<", TokenType.Operator}, {">", TokenType.Operator}, {"!", TokenType.Operator},
            {"+", TokenType.Operator}, {"-", TokenType.Operator}, {"*", TokenType.Operator}, {"/", TokenType.Operator},
            {"%", TokenType.Operator}, {"&", TokenType.Operator}, {"|", TokenType.Operator}, {"^", TokenType.Operator},
            {"<=", TokenType.Operator}, {">=", TokenType.Operator}, {"<>", TokenType.Operator}, {"!=", TokenType.Operator},
            {"||", TokenType.Operator}, {".", TokenType.Operator}
        };

        private static readonly HashSet<char> Delimiters = new HashSet<char> { ',', '(', ')', ';' };

        public Tokenizer(string sql)
        {
            _sql = sql ?? throw new ArgumentNullException(nameof(sql));
        }
        public List<Token> Tokenize()
        {

            var tokens = new List<Token>();
            _position = 0;

            while (_position < _sql.Length)
            {
                char currentChar = _sql[_position];

                // Skip whitespace
                if (char.IsWhiteSpace(currentChar))
                {
                    _position++;
                    continue;
                }

                // Single-line comment --
                if (currentChar == '-' && _position + 1 < _sql.Length && _sql[_position + 1] == '-')
                {
                    _position += 2;
                    while (_position < _sql.Length && _sql[_position] != '\n' && _sql[_position] != '\r')
                        _position++;
                    continue;
                }

                // Multi-line comment /* ... */
                if (currentChar == '/' && _position + 1 < _sql.Length && _sql[_position + 1] == '*')
                {
                    int start = _position;
                    _position += 2;
                    bool closed = false;
                    while (_position + 1 < _sql.Length)
                    {
                        if (_sql[_position] == '*' && _sql[_position + 1] == '/')
                        {
                            _position += 2;
                            closed = true;
                            break;
                        }
                        _position++;
                    }
                    if (!closed)
                        throw new SqlParseException("Unterminated multi-line comment", start);
                    continue;
                }

                // Delimiters
                if (Delimiters.Contains(currentChar))
                {
                    tokens.Add(new Token(
                        currentChar == '(' || currentChar == ')' ? TokenType.Parenthesis :
                        currentChar == ',' ? TokenType.Comma : TokenType.Semicolon,
                        currentChar.ToString(), _position, 1));
                    _position++;
                    continue;
                }

                // Operators
                bool matchedOperator = false;
                foreach (var op in Operators.Keys.OrderByDescending(k => k.Length))
                {
                    if (_position + op.Length <= _sql.Length && _sql.Substring(_position, op.Length) == op)
                    {
                        tokens.Add(new Token(Operators[op], op, _position, op.Length));
                        _position += op.Length;
                        matchedOperator = true;
                        break;
                    }
                }
                if (matchedOperator) continue;

                // N'...' Unicode string literal (T-SQL)
                if ((currentChar == 'N' || currentChar == 'n') &&
                    _position + 1 < _sql.Length &&
                    _sql[_position + 1] == '\'')
                {
                    int start = _position;
                    _position++; // consume N prefix
                    tokens.Add(new Token(TokenType.StringLiteral, ReadSingleQuotedLiteral(start), start, _position - start, isQuoted: false, isUnicodeString: true));
                    continue;
                }

                // Identifiers and keywords (unquoted)
                if (char.IsLetter(currentChar) || currentChar == '_')
                {
                    int start = _position;
                    while (_position < _sql.Length && (char.IsLetterOrDigit(_sql[_position]) || _sql[_position] == '_'))
                        _position++;
                    string value = _sql.Substring(start, _position - start);
                    tokens.Add(new Token(
                        Keywords.Contains(value) ? TokenType.Keyword : TokenType.Identifier,
                        value, start, value.Length));
                    continue;
                }

                // Numeric literals
                if (char.IsDigit(currentChar))
                {
                    int start = _position;
                    while (_position < _sql.Length && char.IsDigit(_sql[_position]))
                        _position++;
                    if (_position < _sql.Length && _sql[_position] == '.')
                    {
                        _position++;
                        while (_position < _sql.Length && char.IsDigit(_sql[_position]))
                            _position++;
                    }
                    string value = _sql.Substring(start, _position - start);
                    tokens.Add(new Token(TokenType.NumericLiteral, value, start, value.Length));
                    continue;
                }

                // Single-quoted string literal
                if (currentChar == '\'')
                {
                    int start = _position;
                    tokens.Add(new Token(TokenType.StringLiteral, ReadSingleQuotedLiteral(start), start, _position - start));
                    continue;
                }

                // Double-quoted delimited identifier
                if (currentChar == '"')
                {
                    int start = _position;
                    _position++;
                    var sb = new StringBuilder();

                    while (_position < _sql.Length)
                    {
                        if (_sql[_position] == '"')
                        {
                            if (_position + 1 < _sql.Length && _sql[_position + 1] == '"')
                            {
                                sb.Append('"');
                                _position += 2;
                            }
                            else
                            {
                                _position++;
                                break;
                            }
                        }
                        else
                        {
                            sb.Append(_sql[_position]);
                            _position++;
                        }
                    }

                    tokens.Add(new Token(TokenType.Identifier, sb.ToString(), start, _position - start, QuoteStyle.DoubleQuote));
                    continue;
                }

                // Square bracket delimited identifier: [Name]]With]]Bracket]
                if (currentChar == '[')
                {
                    int start = _position;
                    _position++;
                    var sb = new StringBuilder();
                    bool closed = false;

                    while (_position < _sql.Length)
                    {
                        if (_sql[_position] == ']')
                        {
                            if (_position + 1 < _sql.Length && _sql[_position + 1] == ']')
                            {
                                sb.Append(']');
                                _position += 2;
                            }
                            else
                            {
                                _position++;
                                closed = true;
                                break;
                            }
                        }
                        else
                        {
                            sb.Append(_sql[_position]);
                            _position++;
                        }
                    }

                    if (!closed)
                        throw new SqlParseException($"Unterminated bracketed identifier starting at {start}", start);

                    tokens.Add(new Token(TokenType.Identifier, sb.ToString(), start, _position - start, QuoteStyle.SquareBracket));
                    continue;
                }

                // Unknown character
                throw new SqlParseException($"Unexpected character '{currentChar}' at position {_position}", _position);
            }

            tokens.Add(new Token(TokenType.EOF, string.Empty, _position, 0));

            return tokens;
        }

        private string ReadSingleQuotedLiteral(int start)
        {
            _position++; // consume opening single quote
            var sb = new StringBuilder();
            bool closed = false;

            while (_position < _sql.Length)
            {
                if (_sql[_position] == '\'')
                {
                    if (_position + 1 < _sql.Length && _sql[_position + 1] == '\'')
                    {
                        sb.Append('\'');
                        _position += 2;
                    }
                    else
                    {
                        _position++;
                        closed = true;
                        break;
                    }
                }
                else
                {
                    sb.Append(_sql[_position]);
                    _position++;
                }
            }

            if (!closed)
                throw new SqlParseException($"Unterminated string literal starting at {start}", start);

            return sb.ToString();
        }

    }
}
