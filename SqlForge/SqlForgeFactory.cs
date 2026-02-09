using System;
using SqlForge.Enums;
using SqlForge.Formatters;
using SqlForge.Interfaces;
using SqlForge.Parsers;
using SqlForge.Reconstructors;

namespace SqlForge
{
    public static class SqlForgeFactory
    {
        public static ISqlParser CreateParser(SqlDialect dialect)
        {
            switch (dialect)
            {
                case SqlDialect.SqlAnywhere:
                    return CreateSqlAnywhereParser();
                case SqlDialect.MsSqlServer:
                    return CreateMsSqlServerParser();
                default:
                    throw new NotSupportedException($"Dialect {dialect} not supported");
            }
        }

        public static ISqlReconstructor CreateReconstructor(SqlDialect dialect)
        {
            switch (dialect)
            {
                case SqlDialect.SqlAnywhere:
                    return new SqlAnywhereReconstructor();
                case SqlDialect.MsSqlServer:
                    return new MsSqlServerReconstructor();
                default:
                    throw new NotSupportedException($"Dialect {dialect} not supported");
            }
        }

        public static ISqlFormatter CreateFormatter(SqlDialect dialect)
        {
            switch (dialect)
            {
                case SqlDialect.SqlAnywhere:
                    return new SqlAnywhereFormatter();
                case SqlDialect.MsSqlServer:
                    return new MsSqlServerFormatter();
                default:
                    throw new NotSupportedException($"Dialect {dialect} not supported");
            }
        }

        private static ISqlParser CreateSqlAnywhereParser()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            var expressionParser = new SqlAnywhereExpressionParser(factoryHolder);
            var selectParser = new SelectStatementParser(expressionParser, factoryHolder);
            var factory = new SqlAnywhereStatementParserFactory(selectParser);
            factoryHolder.SetActualFactory(factory);
            return new SqlAnywhereParser(factory);
        }

        private static ISqlParser CreateMsSqlServerParser()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            var expressionParser = new MsSqlExpressionParser(factoryHolder);
            var selectParser = new MsSqlSelectStatementParser(expressionParser, factoryHolder);
            var factory = new MsSqlServerStatementParserFactory(selectParser);
            factoryHolder.SetActualFactory(factory);
            return new MsSqlServerParser(factory);
        }
    }
}
