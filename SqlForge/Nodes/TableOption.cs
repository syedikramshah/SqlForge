namespace SqlForge.Nodes
{
    public class TableOption : AbstractSqlNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool IsStringValue { get; set; }
    }
}
