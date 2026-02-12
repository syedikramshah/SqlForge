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
    public class MySqlReconstructorTests
    {
        private ISqlParser _parser;
        private ISqlReconstructor _reconstructor;

        [TestInitialize]
        public void Setup()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            IExpressionParser expressionParser = new MySqlExpressionParser(factoryHolder);
            var selectParser = new MySqlSelectStatementParser(expressionParser, factoryHolder);
            IStatementParserFactory factory = new MySqlStatementParserFactory(selectParser, expressionParser);
            factoryHolder.SetActualFactory(factory);
            _parser = new MySqlParser(factory);
            _reconstructor = new MySqlReconstructor();
        }

        [TestMethod]
        public void Reconstruct_LimitClause_MatchesOriginalShape()
        {
            var sql = "SELECT id FROM users ORDER BY id LIMIT 5 OFFSET 10;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);
            var expected = "SELECT id FROM users ORDER BY id ASC LIMIT 5 OFFSET 10;";
            Assert.AreEqual(Normalize(expected), Normalize(reconstructed));
        }

        [TestMethod]
        public void Reconstruct_LimitCommaSyntax_UsesOffsetForm()
        {
            var sql = "SELECT id FROM users LIMIT 10, 5;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);
            var expected = "SELECT id FROM users LIMIT 5 OFFSET 10;";

            Assert.AreEqual(Normalize(expected), Normalize(reconstructed));
        }

        [TestMethod]
        public void Reconstruct_BacktickIdentifiers_Preserved()
        {
            var sql = "SELECT `User ID` FROM `Users`;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "`User ID`");
            StringAssert.Contains(reconstructed, "`Users`");
        }

        [TestMethod]
        public void Reconstruct_QuotedFallback_DefaultsToBackticks()
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

            var reconstructed = _reconstructor.Reconstruct(statement, SqlDialect.MySql);
            StringAssert.Contains(reconstructed, "`User ID`");
        }

        [TestMethod]
        public void Reconstruct_Cte_MatchesOriginalShape()
        {
            var sql = "WITH cte AS (SELECT id FROM users) SELECT id FROM cte;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "WITH `cte` AS");
            StringAssert.Contains(reconstructed, "SELECT id FROM cte");
        }

        [TestMethod]
        public void Reconstruct_UnicodeLiteral_DropsNPrefix()
        {
            var sql = "SELECT id FROM users WHERE name = N'Hello';";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.DoesNotMatch(reconstructed, new Regex("N'Hello'", RegexOptions.IgnoreCase));
            StringAssert.Contains(reconstructed, "'Hello'");
        }

        [TestMethod]
        public void Reconstruct_WindowFunction_MatchesOriginalShape()
        {
            var sql = "SELECT SUM(amount) OVER (ORDER BY id ROWS BETWEEN 1 PRECEDING AND CURRENT ROW) FROM orders;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "OVER (ORDER BY");
            StringAssert.Contains(reconstructed, "ROWS BETWEEN 1 PRECEDING AND CURRENT ROW");
        }

        [TestMethod]
        public void Reconstruct_NullSafeEqual_Preserved()
        {
            var sql = "SELECT id FROM users WHERE name <=> 'a';";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "name <=> 'a'");
        }

        [TestMethod]
        public void Reconstruct_Between_Preserved()
        {
            var sql = "SELECT id FROM users WHERE age BETWEEN 18 AND 65;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "age BETWEEN 18 AND 65");
        }

        [TestMethod]
        public void Reconstruct_NotLike_Preserved()
        {
            var sql = "SELECT id FROM users WHERE name NOT LIKE 'a%';";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "name NOT LIKE 'a%'");
        }

        [TestMethod]
        public void Reconstruct_SelectModifiers_Preserved()
        {
            var sql = "SELECT SQL_CALC_FOUND_ROWS HIGH_PRIORITY id FROM users;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "SQL_CALC_FOUND_ROWS");
            StringAssert.Contains(reconstructed, "HIGH_PRIORITY");
        }

        [TestMethod]
        public void Reconstruct_SelectIntoOutfile_Preserved()
        {
            var sql = "SELECT id INTO OUTFILE 'out.txt' FROM users;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "INTO OUTFILE 'out.txt'");
        }

        [TestMethod]
        public void Reconstruct_InsertValues_Preserved()
        {
            var sql = "INSERT INTO users (id, name) VALUES (1, 'a');";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            Assert.AreEqual(Normalize(sql), Normalize(reconstructed));
        }

        [TestMethod]
        public void Reconstruct_Update_Preserved()
        {
            var sql = "UPDATE users SET name = 'a' WHERE id = 1 LIMIT 1;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "UPDATE users SET name = 'a'");
            StringAssert.Contains(reconstructed, "WHERE id = 1");
            StringAssert.Contains(reconstructed, "LIMIT 1");
        }

        [TestMethod]
        public void Reconstruct_Delete_Preserved()
        {
            var sql = "DELETE FROM users WHERE id = 1 ORDER BY id LIMIT 1;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "DELETE FROM users");
            StringAssert.Contains(reconstructed, "WHERE id = 1");
            StringAssert.Contains(reconstructed, "ORDER BY id");
            StringAssert.Contains(reconstructed, "LIMIT 1");
        }

        [TestMethod]
        public void Reconstruct_CreateTable_Preserved()
        {
            var sql = "CREATE TABLE users (id INT NOT NULL, name VARCHAR(20));";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "CREATE TABLE users");
            StringAssert.Contains(reconstructed, "id INT NOT NULL");
            StringAssert.Contains(reconstructed, "name VARCHAR(20)");
        }

        [TestMethod]
        public void Reconstruct_DropTable_Preserved()
        {
            var sql = "DROP TABLE IF EXISTS users, logs;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "DROP TABLE IF EXISTS users, logs");
        }

        [TestMethod]
        public void Reconstruct_AlterTable_Preserved()
        {
            var sql = "ALTER TABLE users ADD COLUMN age INT, DROP COLUMN name;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "ALTER TABLE users");
            StringAssert.Contains(reconstructed, "ADD COLUMN age INT");
            StringAssert.Contains(reconstructed, "DROP COLUMN name");
        }

        [TestMethod]
        public void Reconstruct_CreateTable_WithOptions_Preserved()
        {
            var sql = "CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(20), KEY idx_name (name(10))) ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "ENGINE=InnoDB");
            StringAssert.Contains(reconstructed, "DEFAULT CHARSET=utf8");
            StringAssert.Contains(reconstructed, "KEY `idx_name`");
        }

        [TestMethod]
        public void Reconstruct_AlterTable_ChangeRenameIndex_Preserved()
        {
            var sql = "ALTER TABLE users CHANGE COLUMN name full_name VARCHAR(50), RENAME TO users_new, ADD INDEX idx_name (full_name), DROP INDEX idx_old;";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.MySql);

            StringAssert.Contains(reconstructed, "CHANGE COLUMN name full_name VARCHAR(50)");
            StringAssert.Contains(reconstructed, "RENAME TO users_new");
            StringAssert.Contains(reconstructed, "ADD KEY `idx_name`");
            StringAssert.Contains(reconstructed, "DROP INDEX `idx_old`");
        }

        [TestMethod]
        public void RoundTrip_CreateTable_WithConstraints_IsStable()
        {
            var sql = "CREATE TABLE users (id INT PRIMARY KEY, parent_id INT, " +
                      "CONSTRAINT fk_users_parent FOREIGN KEY (parent_id) REFERENCES users(id) ON DELETE SET NULL, " +
                      "CHECK (id > 0));";
            var ast1 = _parser.Parse(sql);
            var recon1 = _reconstructor.Reconstruct(ast1, SqlDialect.MySql);
            var ast2 = _parser.Parse(recon1, SqlDialect.MySql);
            var recon2 = _reconstructor.Reconstruct(ast2, SqlDialect.MySql);

            Assert.AreEqual(Normalize(recon1), Normalize(recon2));
        }

        [TestMethod]
        public void RoundTrip_CreateIndex_IsStable()
        {
            var sql = "CREATE INDEX idx_users_name ON users (name);";
            var ast1 = _parser.Parse(sql);
            var recon1 = _reconstructor.Reconstruct(ast1, SqlDialect.MySql);
            var ast2 = _parser.Parse(recon1, SqlDialect.MySql);
            var recon2 = _reconstructor.Reconstruct(ast2, SqlDialect.MySql);

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
