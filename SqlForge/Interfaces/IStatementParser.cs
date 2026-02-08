using SqlForge.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Interfaces
{
    public interface IStatementParser
    {
        bool CanParse(IParserContext context); // Checks if this parser can handle the current token sequence
        SqlStatement Parse(IParserContext context); // Parses the statement and returns the AST node
    }
}
