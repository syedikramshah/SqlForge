using SqlForge.Enums;

namespace SqlForge.Nodes
{
    public class WindowFrame : AbstractSqlNode
    {
        public WindowFrameType Type { get; set; }
        public WindowFrameBound StartBound { get; set; }
        public WindowFrameBound EndBound { get; set; }
    }
}
