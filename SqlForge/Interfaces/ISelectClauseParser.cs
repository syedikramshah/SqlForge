using SqlForge.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Interfaces
{
    public interface ISelectClauseParser
    {
        bool CanParse(IParserContext context);
    }
}
