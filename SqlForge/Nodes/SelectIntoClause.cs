namespace SqlForge.Nodes
{
    public class SelectIntoClause : AbstractSqlNode
    {
        public SelectIntoType Type { get; set; }
        public string FilePath { get; set; }
    }
}
