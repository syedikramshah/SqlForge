using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;
using SqlForge.Enums;

namespace SqlForge.Nodes
{
    public class SetOperatorExpression : ISqlNode
    {
        public ISqlNode Left { get; set; }
        public ISqlNode Right { get; set; }
        public SetOperatorType Operator { get; set; }
    }

}
