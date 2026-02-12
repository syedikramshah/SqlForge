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
                case SqlDialect.MySql:
                    return CreateMySqlParser();
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
                case SqlDialect.MySql:
                    return new MySqlReconstructor();
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
                case SqlDialect.MySql:
                    return new MySqlFormatter();
                default:
                    throw new NotSupportedException($"Dialect {dialect} not supported");
            }
        }

        private static ISqlParser CreateSqlAnywhereParser()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            var expressionParser = new SqlAnywhereExpressionParser(factoryHolder);
            var selectParser = new SelectStatementParser(expressionParser, factoryHolder);
            var factory = new SqlAnywhereStatementParserFactory(selectParser, expressionParser);
            factoryHolder.SetActualFactory(factory);
            return new SqlAnywhereParser(factory);
        }

        private static ISqlParser CreateMsSqlServerParser()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            var expressionParser = new MsSqlExpressionParser(factoryHolder);
            var selectParser = new MsSqlSelectStatementParser(expressionParser, factoryHolder);
            var factory = new MsSqlServerStatementParserFactory(selectParser, expressionParser);
            factoryHolder.SetActualFactory(factory);
            return new MsSqlServerParser(factory);
        }

        private static ISqlParser CreateMySqlParser()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            var expressionParser = new MySqlExpressionParser(factoryHolder);
            var selectParser = new MySqlSelectStatementParser(expressionParser, factoryHolder);
            var factory = new MySqlStatementParserFactory(selectParser, expressionParser);
            factoryHolder.SetActualFactory(factory);
            return new MySqlParser(factory);
        }
    }
}
