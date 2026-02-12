using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Formatters;
using SqlForge.Interfaces;
using SqlForge.Parsers;
using System.Text.RegularExpressions;

namespace SqlForge.Tests
{
    [TestClass]
    public class MySqlFormatterTests
    {
        private ISqlParser _parser;
        private ISqlFormatter _formatter;

        [TestInitialize]
        public void Setup()
        {
            var statementParserFactoryHolder = new StatementParserFactoryHolder();
            IExpressionParser expressionParser = new MySqlExpressionParser(statementParserFactoryHolder);
            var selectParser = new MySqlSelectStatementParser(expressionParser, statementParserFactoryHolder);
            IStatementParserFactory actualStatementParserFactory = new MySqlStatementParserFactory(selectParser, expressionParser);
            statementParserFactoryHolder.SetActualFactory(actualStatementParserFactory);
            _parser = new MySqlParser(actualStatementParserFactory);

            _formatter = new MySqlFormatter();
        }

        private string NormalizeFormattedOutput(string formatted)
        {
            formatted = Regex.Replace(formatted, @"^\s*$\n|\r", "", RegexOptions.Multiline).Trim();
            formatted = Regex.Replace(formatted, @"(?<!^)\s{2,}", " ", RegexOptions.Multiline);
            return formatted;
        }

        [TestMethod]
        public void Format_LimitClause_ProducesExpectedNodes()
        {
            var sql = "SELECT id FROM users LIMIT 5 OFFSET 10;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            StringAssert.Contains(formatted, "LimitClause:");
            StringAssert.Contains(formatted, "CountExpression:");
            StringAssert.Contains(formatted, "OffsetExpression:");
        }

        [TestMethod]
        public void Format_BacktickIdentifiers_PreservedInOutput()
        {
            var sql = "SELECT `User ID` AS `U` FROM `Users` AS `t`;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            var normalized = NormalizeFormattedOutput(formatted);
            StringAssert.Contains(normalized, "Name: `User ID`");
            StringAssert.Contains(normalized, "Alias: `U`");
            StringAssert.Contains(normalized, "TableName: `Users` (Alias: `t`)");
        }

        [TestMethod]
        public void Format_InsertStatement_ProducesExpectedNodes()
        {
            var sql = "INSERT INTO users (id, name) VALUES (1, 'a');";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            StringAssert.Contains(formatted, "InsertStatement:");
            StringAssert.Contains(formatted, "Columns:");
            StringAssert.Contains(formatted, "Values:");
        }

        [TestMethod]
        public void Format_CreateTable_ProducesExpectedNodes()
        {
            var sql = "CREATE TABLE users (id INT, name VARCHAR(20));";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            StringAssert.Contains(formatted, "CreateTableStatement:");
            StringAssert.Contains(formatted, "Columns:");
        }
    }
}
