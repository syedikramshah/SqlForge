using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class AssignmentExpression : AbstractSqlNode
    {
        public ColumnExpression Column { get; set; }
        public ISqlNode Value { get; set; }
    }
}
