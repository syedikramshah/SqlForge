using SqlForge.Enums;
using SqlForge.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Interfaces
{
    public interface IParserContext
    {
        Token PeekToken(int offset = 0);
        Token CurrentToken();
        Token ConsumeToken();
        Token ConsumeToken(string expectedValue, TokenType expectedType = TokenType.Keyword);
        bool MatchToken(string value, TokenType type = TokenType.Keyword);
        bool IsKeyword(string keyword);
        // We might add more utility methods here as needed by parsers (e.g., to handle errors)
    }
}
