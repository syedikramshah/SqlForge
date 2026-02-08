using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Parsers
{

    /// <summary>
    /// Base class for SQL parsers, providing common utility methods.
    /// (Note: Token handling methods have been moved to IParserContext/ParserContext)
    /// </summary>
    public abstract class BaseSqlParser : ISqlParser
    {
        // These fields are no longer needed here, as ParserContext will manage them.
        // protected List<Token> _tokens;
        // protected int _currentTokenIndex;

        public abstract SqlStatement Parse(string sql, SqlDialect dialect = SqlDialect.Generic);

        // All the token-handling methods (PeekToken, CurrentToken, ConsumeToken, MatchToken, IsKeyword)
        // that were here previously have been moved to ParserContext.
        // So, you can remove them from this class.
    }

}