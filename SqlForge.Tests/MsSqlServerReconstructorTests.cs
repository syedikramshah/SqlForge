using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Parsers;
using SqlForge.Reconstructors;
using System.Text.RegularExpressions;

namespace SqlForge.Tests
{
    [TestClass]
    public class MsSqlServerReconstructorTests
    {
        private ISqlParser _parser;
        private ISqlReconstructor _reconstructor;

        [TestInitialize]
        public void Setup()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            IExpressionParser expressionParser = new MsSqlExpressionParser(factoryHolder);
            var selectParser = new MsSqlSelectStatementParser(expressionParser, factoryHolder);
            IStatementParserFactory factory = new MsSqlServerStatementParserFactory(selectParser);
            factoryHolder.SetActualFactory(factory);
            _parser = new MsSqlServerParser(factory);
            _reconstructor = new MsSqlServerReconstructor();
        }

        [TestMethod]
        public void Reconstruct_TopClause_MatchesOriginal()
        {
            var sql = "SELECT TOP 10 id FROM users;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);
            Assert.AreEqual(Normalize(sql), Normalize(reconstructed));
        }

        [TestMethod]
        public void Reconstruct_SquareBrackets_Preserved()
        {
            var sql = "SELECT [User ID] FROM [dbo].[Users];";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);

            StringAssert.Contains(reconstructed, "[User ID]");
            StringAssert.Contains(reconstructed, "[dbo].[Users]");
        }

        [TestMethod]
        public void Reconstruct_QuotedFallback_DefaultsToBrackets()
        {
            var statement = new SqlStatement
            {
                Type = StatementType.Select,
                Body = new SelectStatement
                {
                    SelectItems = new System.Collections.Generic.List<SelectExpression>
                    {
                        new SelectExpression
                        {
                            Expression = new ColumnExpression
                            {
                                ColumnName = "User ID",
                                ColumnNameQuoted = true,
                                ColumnQuoteStyle = QuoteStyle.None
                            }
                        }
                    },
                    FromClause = new FromClause
                    {
                        TableExpressions = new System.Collections.Generic.List<SqlForge.Interfaces.ISqlNode>
                        {
                            new TableExpression
                            {
                                TableName = "Users"
                            }
                        }
                    }
                }
            };

            var reconstructed = _reconstructor.Reconstruct(statement, SqlDialect.MsSqlServer);
            StringAssert.Contains(reconstructed, "[User ID]");
        }

        [TestMethod]
        public void Reconstruct_TableHints_MatchesOriginal()
        {
            var sql = "SELECT id FROM users WITH (NOLOCK, INDEX(ix_users));";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);
            Assert.AreEqual(Normalize(sql), Normalize(reconstructed));
        }

        [TestMethod]
        public void Reconstruct_WindowFunction_MatchesOriginalShape()
        {
            var sql = "SELECT SUM(amount) OVER (ORDER BY id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) FROM orders;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);

            StringAssert.Contains(reconstructed, "OVER (ORDER BY");
            StringAssert.Contains(reconstructed, "ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW");
        }

        [TestMethod]
        public void Reconstruct_Cte_MatchesOriginalShape()
        {
            var sql = "WITH cte AS (SELECT id FROM users) SELECT id FROM cte;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);

            StringAssert.Contains(reconstructed, "WITH [cte] AS");
            StringAssert.Contains(reconstructed, "SELECT id FROM cte");
        }

        [TestMethod]
        public void Reconstruct_NotIn_UsesNotInForm()
        {
            var sql = "SELECT id FROM users WHERE id NOT IN (1, 2, 3);";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);

            StringAssert.Contains(reconstructed.ToUpperInvariant(), "NOT IN");
        }

        [TestMethod]
        public void Reconstruct_BooleanPrecedence_PreservesOrGrouping()
        {
            var sql = "SELECT id FROM users WHERE (active = 1 OR role = 'admin') AND deleted_at IS NULL;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);

            StringAssert.Contains(reconstructed, "(active = 1 OR role = 'admin') AND deleted_at IS NULL");
        }

        [TestMethod]
        public void Reconstruct_UnicodeLiteral_PreservesNPrefix()
        {
            var sql = "SELECT id FROM users WHERE bio = N'Hello';";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);

            StringAssert.Contains(reconstructed, "N'Hello'");
        }

        [TestMethod]
        public void RoundTrip_ComplexQuery_IsStable()
        {
            var sql = "WITH cte AS (SELECT TOP 10 id FROM users) SELECT SUM(amount) OVER (ORDER BY id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS total FROM cte ORDER BY id OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;";
            var ast1 = _parser.Parse(sql);
            var recon1 = _reconstructor.Reconstruct(ast1, SqlDialect.MsSqlServer);
            var ast2 = _parser.Parse(recon1);
            var recon2 = _reconstructor.Reconstruct(ast2, SqlDialect.MsSqlServer);

            Assert.AreEqual(Normalize(recon1), Normalize(recon2));
        }

        [TestMethod]
        public void RoundTrip_CrossApply_SubqueryAlias_IsStable()
        {
            var sql = "SELECT u.id, x.last_order_id FROM users u CROSS APPLY (SELECT TOP 1 o.id AS last_order_id FROM orders o WHERE o.user_id = u.id ORDER BY o.id DESC) x;";
            var ast1 = _parser.Parse(sql);
            var recon1 = _reconstructor.Reconstruct(ast1, SqlDialect.MsSqlServer);
            var ast2 = _parser.Parse(recon1, SqlDialect.MsSqlServer);
            var recon2 = _reconstructor.Reconstruct(ast2, SqlDialect.MsSqlServer);

            Assert.AreEqual(Normalize(recon1), Normalize(recon2));
        }

        private static string Normalize(string sql)
        {
            var norm = sql.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            norm = Regex.Replace(norm, "\\s+", " ").Trim().TrimEnd(';').ToUpperInvariant();
            return norm;
        }
    }
}
