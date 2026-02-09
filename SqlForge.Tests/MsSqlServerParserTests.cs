using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Parsers;

namespace SqlForge.Tests
{
    [TestClass]
    public class MsSqlServerParserTests
    {
        private ISqlParser _parser;

        [TestInitialize]
        public void Setup()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            IExpressionParser expressionParser = new MsSqlExpressionParser(factoryHolder);
            var selectParser = new MsSqlSelectStatementParser(expressionParser, factoryHolder);
            IStatementParserFactory factory = new MsSqlServerStatementParserFactory(selectParser);
            factoryHolder.SetActualFactory(factory);
            _parser = new MsSqlServerParser(factory);
        }

        [TestMethod]
        public void Parse_TopClause_Succeeds()
        {
            var ast = _parser.Parse("SELECT TOP 10 id FROM users;");
            var select = (SelectStatement)ast.Body;

            Assert.IsNotNull(select.TopClause);
            Assert.AreEqual("10", ((LiteralExpression)select.TopClause.Expression).Value);
        }

        [TestMethod]
        public void Parse_OffsetWithoutOrderBy_Throws()
        {
            Assert.ThrowsException<SqlParseException>(() =>
                _parser.Parse("SELECT id FROM users OFFSET 10 ROWS;"));
        }

        [TestMethod]
        public void Parse_TopAndOffsetTogether_Throws()
        {
            Assert.ThrowsException<SqlParseException>(() =>
                _parser.Parse("SELECT TOP 5 id FROM users ORDER BY id OFFSET 1 ROWS;"));
        }

        [TestMethod]
        public void Parse_TopWithTiesWithoutOrderBy_Throws()
        {
            Assert.ThrowsException<SqlParseException>(() =>
                _parser.Parse("SELECT TOP 5 WITH TIES id FROM users;"));
        }

        [TestMethod]
        public void Parse_ExceptAll_Throws()
        {
            Assert.ThrowsException<SqlParseException>(() =>
                _parser.Parse("SELECT id FROM a EXCEPT ALL SELECT id FROM b;"));
        }

        [TestMethod]
        public void Parse_IntersectAll_Throws()
        {
            Assert.ThrowsException<SqlParseException>(() =>
                _parser.Parse("SELECT id FROM a INTERSECT ALL SELECT id FROM b;"));
        }

        [TestMethod]
        public void Parse_ApplyJoin_SetsJoinTypeAndNoOnCondition()
        {
            var ast = _parser.Parse(
                "SELECT u.id FROM users u CROSS APPLY (SELECT TOP 1 o.id AS last_order_id FROM orders o WHERE o.user_id = u.id ORDER BY o.id DESC) x;");

            var select = (SelectStatement)ast.Body;
            var join = (JoinExpression)select.FromClause.TableExpressions[0];

            Assert.AreEqual(JoinType.CrossApply, join.Type);
            Assert.IsNull(join.OnCondition);
        }

        [TestMethod]
        public void Parse_OuterApply_WithAliasAndAsInSubquerySelectList_Succeeds()
        {
            var ast = _parser.Parse(
                "SELECT u.id FROM users u OUTER APPLY (SELECT TOP 1 o.id AS last_order_id FROM orders o WHERE o.user_id = u.id ORDER BY o.id DESC) AS x;");

            var select = (SelectStatement)ast.Body;
            var join = (JoinExpression)select.FromClause.TableExpressions[0];

            Assert.AreEqual(JoinType.OuterApply, join.Type);
            Assert.IsNull(join.OnCondition);
        }

        [TestMethod]
        public void Parse_FullApplyQueryWithOuterSelectAlias_Succeeds()
        {
            var ast = _parser.Parse(
                "SELECT u.id, x.last_order_id FROM users u CROSS APPLY (SELECT TOP 1 o.id AS last_order_id FROM orders o WHERE o.user_id = u.id ORDER BY o.id DESC) x;");

            var select = (SelectStatement)ast.Body;
            Assert.AreEqual(2, select.SelectItems.Count);
            var join = (JoinExpression)select.FromClause.TableExpressions[0];
            Assert.AreEqual(JoinType.CrossApply, join.Type);
        }

        [TestMethod]
        public void Parse_Cte_SetsWithClause()
        {
            var ast = _parser.Parse("WITH cte AS (SELECT id FROM users) SELECT id FROM cte;");

            Assert.IsNotNull(ast.WithClause);
            Assert.AreEqual(1, ast.WithClause.CommonTableExpressions.Count);
            Assert.AreEqual("cte", ast.WithClause.CommonTableExpressions[0].Name);
        }

        [TestMethod]
        public void Parse_WindowFunction_WithAggregateOver_ParsesAsWindowFunction()
        {
            var ast = _parser.Parse("SELECT SUM(amount) OVER (ORDER BY id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) FROM orders;");
            var select = (SelectStatement)ast.Body;
            var window = (WindowFunctionExpression)select.SelectItems[0].Expression;

            Assert.AreEqual("SUM", window.FunctionName);
            Assert.IsNotNull(window.Frame);
            Assert.AreEqual(WindowFrameType.Rows, window.Frame.Type);
        }

        [TestMethod]
        public void Parse_WindowFunction_GroupsFrame_ParsesFrameType()
        {
            var ast = _parser.Parse("SELECT SUM(amount) OVER (ORDER BY id GROUPS BETWEEN 1 PRECEDING AND CURRENT ROW) FROM orders;");
            var select = (SelectStatement)ast.Body;
            var window = (WindowFunctionExpression)select.SelectItems[0].Expression;

            Assert.AreEqual(WindowFrameType.Groups, window.Frame.Type);
            Assert.AreEqual(WindowFrameBoundType.Preceding, window.Frame.StartBound.Type);
            Assert.AreEqual(WindowFrameBoundType.CurrentRow, window.Frame.EndBound.Type);
        }

        [TestMethod]
        public void Parse_BracketedIdentifiers_PreservesQuoteStyles()
        {
            var ast = _parser.Parse("SELECT [User ID] FROM [dbo].[Users];");
            var select = (SelectStatement)ast.Body;
            var col = (ColumnExpression)select.SelectItems[0].Expression;
            var table = (TableExpression)select.FromClause.TableExpressions[0];

            Assert.AreEqual(QuoteStyle.SquareBracket, col.ColumnQuoteStyle);
            Assert.AreEqual(QuoteStyle.SquareBracket, table.SchemaQuoteStyle);
            Assert.AreEqual(QuoteStyle.SquareBracket, table.TableQuoteStyle);
        }
    }
}
