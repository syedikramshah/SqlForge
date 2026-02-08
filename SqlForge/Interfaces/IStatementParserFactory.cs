using SqlForge.Nodes;

namespace SqlForge.Interfaces
{
    public interface IStatementParserFactory
    {
        IStatementParser GetParserForStatementType(IParserContext context);
        SqlStatement ParseStatement(IParserContext context);
    }
}