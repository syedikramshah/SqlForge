using SqlForge.Interfaces;

namespace SqlForge.Nodes
{
    public class IndexColumn : AbstractSqlNode
    {
        public ColumnExpression Column { get; set; }
        public ISqlNode LengthExpression { get; set; }
        public bool? IsAscending { get; set; }
    }
}
