using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlForge.Interfaces;

namespace SqlForge.Formatters
{
    /// <summary>
    /// Base class for SQL formatters.
    /// </summary>
    public abstract class BaseSqlFormatter : ISqlFormatter
    {
        public abstract string Format(ISqlNode node);
    }

}
