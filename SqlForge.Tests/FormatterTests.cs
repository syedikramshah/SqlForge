using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Formatters;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Parsers;
using System.Text.RegularExpressions;

namespace SqlForge.Tests
{
    [TestClass]
    public class FormatterTests
    {
        private ISqlParser _parser;
        private ISqlFormatter _formatter;

        [TestInitialize]
        public void Setup()
        {
            // --- NEW DEPENDENCY INJECTION SETUP (Identical to ParserTests.cs) ---
            var statementParserFactoryHolder = new StatementParserFactoryHolder();
            IExpressionParser expressionParser = new SqlAnywhereExpressionParser(statementParserFactoryHolder);
            IStatementParser selectStatementParser = new SelectStatementParser(expressionParser, statementParserFactoryHolder);
            IStatementParserFactory actualStatementParserFactory = new SqlAnywhereStatementParserFactory(
                (SelectStatementParser)selectStatementParser
            );
            statementParserFactoryHolder.SetActualFactory(actualStatementParserFactory);
            _parser = new SqlAnywhereParser(actualStatementParserFactory);

            _formatter = new SqlAnywhereFormatter(); // Formatter instantiation remains simple
        }

        private string NormalizeFormattedOutput(string formatted)
        {
            formatted = Regex.Replace(formatted, @"^\s*$\n|\r", "", RegexOptions.Multiline).Trim();
            formatted = Regex.Replace(formatted, @"(?<!^)\s{2,}", " ", RegexOptions.Multiline);
            return formatted;
        }

        [TestMethod]
        public void Format_SimpleSelect_ProducesExpectedOutput()
        {
            var sql = "SELECT Col1, Col2 FROM MyTable;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            var expected = @"SqlStatement:
    Type: Select
    SelectStatement:
        SelectItems:
            SelectExpression:
                Expression:
                    ColumnExpression:
                        Name: Col1
            SelectExpression:
                Expression:
                    ColumnExpression:
                        Name: Col2
        FromClause:
            TableExpressions:
                TableExpression:
                    TableName: MyTable";

            Assert.AreEqual(NormalizeFormattedOutput(expected), NormalizeFormattedOutput(formatted));
        }

        [TestMethod]
        public void Format_SelectWithAliases_ProducesExpectedOutput()
        {
            var sql = "SELECT u.UserID AS ID, u.UserName FROM Users AS u;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            var expected = @"SqlStatement:
    Type: Select
    SelectStatement:
        SelectItems:
            SelectExpression:
                Expression:
                    ColumnExpression:
                        Name: u.UserID
                Alias: ID
            SelectExpression:
                Expression:
                    ColumnExpression:
                        Name: u.UserName
        FromClause:
            TableExpressions:
                TableExpression:
                    TableName: Users (Alias: u)";

            Assert.AreEqual(NormalizeFormattedOutput(expected), NormalizeFormattedOutput(formatted));
        }

        [TestMethod]
        public void Format_WhereClause_ProducesExpectedOutput()
        {
            var sql = "SELECT Col FROM MyTable WHERE Status = 'Active' AND Value > 100;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            var expected = @"SqlStatement:
    Type: Select
    SelectStatement:
        SelectItems:
            SelectExpression:
                Expression:
                    ColumnExpression:
                        Name: Col
        FromClause:
            TableExpressions:
                TableExpression:
                    TableName: MyTable
        WhereClause:
            Condition:
                BinaryExpression:
                    Left:
                        BinaryExpression:
                            Left:
                                ColumnExpression:
                                    Name: Status
                            Operator: =
                            Right:
                                LiteralExpression:
                                    Value: Active (Type: String)
                    Operator: AND
                    Right:
                        BinaryExpression:
                            Left:
                                ColumnExpression:
                                    Name: Value
                            Operator: >
                            Right:
                                LiteralExpression:
                                    Value: 100 (Type: Number)";

            Assert.AreEqual(NormalizeFormattedOutput(expected), NormalizeFormattedOutput(formatted));
        }

        [TestMethod]
        public void Format_FunctionCall_ProducesExpectedOutput()
        {
            var sql = "SELECT COUNT(*), SUM(Amount) FROM Orders;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            var expected = @"SqlStatement:
    Type: Select
    SelectStatement:
        SelectItems:
            SelectExpression:
                Expression:
                    FunctionCallExpression:
                        FunctionName: COUNT
                        Arguments: *
            SelectExpression:
                Expression:
                    FunctionCallExpression:
                        FunctionName: SUM
                        Arguments:
                            ColumnExpression:
                                Name: Amount
        FromClause:
            TableExpressions:
                TableExpression:
                    TableName: Orders";

            Assert.AreEqual(NormalizeFormattedOutput(expected), NormalizeFormattedOutput(formatted));
        }

        [TestMethod]
        public void Format_NestedSubqueryInSelect_ProducesExpectedOutput()
        {
            var sql = "SELECT (SELECT MAX(x) FROM T1) AS MaxVal FROM T2;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            var expected = @"SqlStatement:
    Type: Select
    SelectStatement:
        SelectItems:
            SelectExpression:
                Expression:
                    SubqueryExpression:
                        Subquery:
                            SqlStatement:
                                Type: Select
                                SelectStatement:
                                    SelectItems:
                                        SelectExpression:
                                            Expression:
                                                FunctionCallExpression:
                                                    FunctionName: MAX
                                                    Arguments:
                                                        ColumnExpression:
                                                            Name: x
                                    FromClause:
                                        TableExpressions:
                                            TableExpression:
                                                TableName: T1
                        Alias: MaxVal
        FromClause:
            TableExpressions:
                TableExpression:
                    TableName: T2";

            Assert.AreEqual(NormalizeFormattedOutput(expected), NormalizeFormattedOutput(formatted));
        }

        [TestMethod]
        public void Format_InnerJoin_ProducesExpectedOutput()
        {
            var sql = "SELECT a.Col1 FROM TableA AS a INNER JOIN TableB AS b ON a.Id = b.Id;";
            var statement = _parser.Parse(sql);
            var formatted = _formatter.Format(statement);

            var expected = @"SqlStatement: Type: Select
    SelectStatement:
        SelectItems:
            SelectExpression:
                Expression:
                    ColumnExpression:
                        Name: a.Col1
        FromClause:
            TableExpressions:
                JoinExpression:
                    JoinType: Inner
                    Left:
                        TableExpression:
                            TableName: TableA (Alias: a)
                    Right:
                        TableExpression:
                            TableName: TableB (Alias: b)
                    OnCondition:
                        BinaryExpression:
                            Left:
                                ColumnExpression:
                                    Name: a.Id
                            Operator: =
                            Right:
                                ColumnExpression:
                                    Name: b.Id";

            Assert.AreEqual(NormalizeFormattedOutput(expected), NormalizeFormattedOutput(formatted));
        }
    }
}