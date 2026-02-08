using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Enums
{


    /// <summary>
    /// Represents the type of a token identified by the tokenizer.
    /// </summary>
    public enum TokenType
    {
        Keyword,
        Identifier,
        StringLiteral,
        NumericLiteral,
        Operator,
        Parenthesis,
        Comma,
        Semicolon,
        EOF // End of file/input
    }
}
