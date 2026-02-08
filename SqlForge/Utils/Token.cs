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
        public bool IsQuoted { get; } // NEW

        public Token(TokenType type, string value, int startIndex = -1, int length = -1, bool isQuoted = false)
        {
            Type = type;
            Value = value;
            StartIndex = startIndex;
            Length = length;
            IsQuoted = isQuoted;
        }

        public override string ToString() =>
            $"[{Type}] '{Value}' ({StartIndex}-{StartIndex + Length})" + (IsQuoted ? " [quoted]" : "");
    }
}
