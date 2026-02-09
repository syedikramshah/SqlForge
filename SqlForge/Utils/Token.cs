using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Enums;

namespace SqlForge.Utils
{
    /// <summary>
    /// Represents a single token extracted from the SQL string.
    /// </summary>
    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int StartIndex { get; }
        public int Length { get; }
        public bool IsQuoted { get; }
        public QuoteStyle QuoteStyle { get; }
        public bool IsDoubleQuoted => QuoteStyle == QuoteStyle.DoubleQuote;
        public bool IsSquareBracketed => QuoteStyle == QuoteStyle.SquareBracket;
        public bool IsUnicodeString { get; }

        public Token(TokenType type, string value, int startIndex = -1, int length = -1, bool isQuoted = false, bool isUnicodeString = false)
        {
            Type = type;
            Value = value;
            StartIndex = startIndex;
            Length = length;
            IsQuoted = isQuoted;
            QuoteStyle = isQuoted ? QuoteStyle.DoubleQuote : QuoteStyle.None;
            IsUnicodeString = isUnicodeString;
        }

        public Token(TokenType type, string value, int startIndex, int length, QuoteStyle quoteStyle, bool isUnicodeString = false)
        {
            Type = type;
            Value = value;
            StartIndex = startIndex;
            Length = length;
            QuoteStyle = quoteStyle;
            IsQuoted = quoteStyle != QuoteStyle.None;
            IsUnicodeString = isUnicodeString;
        }

        public override string ToString() =>
            $"[{Type}] '{Value}' ({StartIndex}-{StartIndex + Length})" + (IsQuoted ? $" [quoted:{QuoteStyle}]" : "");
    }
}
