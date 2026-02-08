using SqlForge.Interfaces;
using SqlForge.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Parsers
{
    public class StatementParserFactoryHolder : IStatementParserFactory
    {
        private IStatementParserFactory _actualFactory;

        public void SetActualFactory(IStatementParserFactory factory)
        {
            _actualFactory = factory;
        }

        // Delegate all calls to the actual factory once it's set.
        public IStatementParser GetParserForStatementType(IParserContext context)
        {
            if (_actualFactory == null)
                throw new InvalidOperationException("IStatementParserFactory not yet set in holder.");
            return _actualFactory.GetParserForStatementType(context);
        }

        public SqlStatement ParseStatement(IParserContext context)
        {
            if (_actualFactory == null)
                throw new InvalidOperationException("IStatementParserFactory not yet set in holder.");
            return _actualFactory.ParseStatement(context);
        }
    }
}
