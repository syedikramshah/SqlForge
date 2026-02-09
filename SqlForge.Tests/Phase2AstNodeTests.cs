using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Nodes;

namespace SqlForge.Tests
{
    [TestClass]
    public class Phase2AstNodeTests
    {
        [TestMethod]
        public void SelectStatement_HasTopAndOffsetFetchProperties()
        {
            var top = new TopClause
            {
                Expression = new LiteralExpression { Value = "10", Type = LiteralType.Number },
                IsPercent = false,
                WithTies = true
            };
            var offset = new OffsetFetchClause
            {
                OffsetExpression = new LiteralExpression { Value = "5", Type = LiteralType.Number },
                FetchExpression = new LiteralExpression { Value = "10", Type = LiteralType.Number },
                IsNext = true
            };

            var select = new SelectStatement
            {
                TopClause = top,
                OffsetFetchClause = offset
            };

            Assert.IsNotNull(select.TopClause);
            Assert.IsNotNull(select.OffsetFetchClause);
            Assert.IsTrue(select.TopClause.WithTies);
            Assert.IsTrue(select.OffsetFetchClause.IsNext);
        }

        [TestMethod]
        public void SqlStatement_HasWithClauseProperty()
        {
            var statement = new SqlStatement
            {
                Type = StatementType.Select,
                Body = new SelectStatement(),
                WithClause = new WithClause()
            };
            statement.WithClause.CommonTableExpressions.Add(new CommonTableExpression
            {
                Name = "cte_users",
                Query = new SqlStatement { Type = StatementType.Select, Body = new SelectStatement() }
            });

            Assert.IsNotNull(statement.WithClause);
            Assert.AreEqual(1, statement.WithClause.CommonTableExpressions.Count);
            Assert.AreEqual("cte_users", statement.WithClause.CommonTableExpressions[0].Name);
        }

        [TestMethod]
        public void QuoteStyleFields_DefaultToNoneAndAreSettable()
        {
            var table = new TableExpression
            {
                SchemaQuoteStyle = QuoteStyle.SquareBracket,
                TableQuoteStyle = QuoteStyle.DoubleQuote,
                AliasQuoteStyle = QuoteStyle.SquareBracket
            };
            var column = new ColumnExpression
            {
                SchemaQuoteStyle = QuoteStyle.DoubleQuote,
                TableAliasQuoteStyle = QuoteStyle.SquareBracket,
                ColumnQuoteStyle = QuoteStyle.DoubleQuote
            };
            var selectExpr = new SelectExpression { AliasQuoteStyle = QuoteStyle.SquareBracket };
            var subquery = new SubqueryExpression { AliasQuoteStyle = QuoteStyle.DoubleQuote };

            Assert.AreEqual(QuoteStyle.SquareBracket, table.SchemaQuoteStyle);
            Assert.AreEqual(QuoteStyle.DoubleQuote, table.TableQuoteStyle);
            Assert.AreEqual(QuoteStyle.SquareBracket, table.AliasQuoteStyle);
            Assert.AreEqual(QuoteStyle.DoubleQuote, column.SchemaQuoteStyle);
            Assert.AreEqual(QuoteStyle.SquareBracket, column.TableAliasQuoteStyle);
            Assert.AreEqual(QuoteStyle.DoubleQuote, column.ColumnQuoteStyle);
            Assert.AreEqual(QuoteStyle.SquareBracket, selectExpr.AliasQuoteStyle);
            Assert.AreEqual(QuoteStyle.DoubleQuote, subquery.AliasQuoteStyle);
        }

        [TestMethod]
        public void JoinType_ContainsApplyVariants()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(JoinType), JoinType.CrossApply));
            Assert.IsTrue(System.Enum.IsDefined(typeof(JoinType), JoinType.OuterApply));
        }

        [TestMethod]
        public void TableExpression_HasTableHintsCollection()
        {
            var table = new TableExpression();
            table.TableHints.Add(new TableHint { HintName = "NOLOCK" });

            Assert.IsNotNull(table.TableHints);
            Assert.AreEqual(1, table.TableHints.Count);
            Assert.AreEqual("NOLOCK", table.TableHints[0].HintName);
        }
    }
}
