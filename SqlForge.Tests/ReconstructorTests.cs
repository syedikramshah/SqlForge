using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Parsers;
using SqlForge.Reconstructors;
using System;
using System.Text.RegularExpressions;

namespace SqlForge.Tests
{
    [TestClass]
    public class ReconstructorTests
    {
        private ISqlParser _parser;
        private ISqlReconstructor _reconstructor;

        [TestInitialize]
        public void Setup()
        {
            // --- NEW DEPENDENCY INJECTION SETUP (Identical to ParserTests.cs) ---
            var statementParserFactoryHolder = new StatementParserFactoryHolder();
            IExpressionParser expressionParser = new SqlAnywhereExpressionParser(statementParserFactoryHolder);
            IStatementParser selectStatementParser = new SelectStatementParser(expressionParser, statementParserFactoryHolder);
            IStatementParserFactory actualStatementParserFactory = new SqlAnywhereStatementParserFactory(
                (SelectStatementParser)selectStatementParser,
                expressionParser
            );
            statementParserFactoryHolder.SetActualFactory(actualStatementParserFactory);
            _parser = new SqlAnywhereParser(actualStatementParserFactory);

            _reconstructor = new SqlAnywhereReconstructor(); // Reconstructor instantiation remains simple
        }

        private string NormalizeSqlForComparison(string sql)
        {
            sql = Regex.Replace(sql, @"--.*$", "", RegexOptions.Multiline);
            sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);

            sql = sql.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

            sql = Regex.Replace(sql, @"\s+", " ");

            sql = sql.Trim();

            sql = Regex.Replace(sql, @"\s*([=<>!+\-*/%,()])\s*", "$1");
            sql = Regex.Replace(sql, @"([=<>!+\-*/%,()])", " $1 ");
            sql = Regex.Replace(sql, @"\s+", " ");

            sql = sql.ToUpperInvariant();

            sql = Regex.Replace(sql, @"(\w+)\s*\(\s*(\*)\s*\)", "$1($2)");
            sql = Regex.Replace(sql, @"(\w+)\s*\(\s*([^\)]+?)\s*\)", "$1($2)");

            return Regex.Replace(sql.Trim().TrimEnd(';'), @"\s+", " ")
                .Replace(" (", "(")
                .Replace("( ", "(")
                .Replace(" )", ")")
                .Replace("= ", "=")
                .Replace(" =", "=")
                .Replace("> =", ">=")
                .Replace("< =", "<=")
                .Replace(" ,", ",")
                .Trim();
        }

        private string Normalize(string sql)
        {
            return NormalizeSqlForComparison(sql);
        }


        [TestMethod]
        public void Reconstruct_SimpleSelect_MatchesOriginal()
        {
            var originalSql = "SELECT Col1, Col2 FROM MyTable;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_SelectWithAliases_MatchesOriginal()
        {
            var originalSql = "SELECT u.UserID AS ID, u.UserName FROM Users AS u;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_WhereClause_MatchesOriginal()
        {
            var originalSql = "SELECT Col FROM MyTable WHERE Status = 'Active' AND Value > 100;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_ArithmeticExpressionInSelect_MatchesOriginal()
        {
            var originalSql = "SELECT Price * Quantity AS Total FROM OrderItems;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_FunctionCall_MatchesOriginal()
        {
            var originalSql = "SELECT COUNT(*), SUM(Amount) FROM Orders;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_NestedSubqueryInSelect_MatchesOriginal()
        {
            var originalSql = "SELECT (SELECT MAX(x) FROM T1) AS MaxVal FROM T2;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_DerivedTableInFrom_MatchesOriginal()
        {
            var originalSql = "SELECT a.Col FROM (SELECT ID AS Col FROM TempTable) AS a WHERE a.Col > 10;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }
        [TestMethod]
        public void Reconstruct_SimpleSelectWithAlias_ShouldPreserveAlias()
        {
            string input = "SELECT WH.\"Well ID\" FROM \"DBA\".\"WellHeader\" AS WH;";
            var statement = _parser.Parse(input);

            var reconstructor = new SqlAnywhereReconstructor();
            string output = reconstructor.Reconstruct(statement);

            Assert.AreEqual(Normalize(input), Normalize(output));
        }

        [TestMethod]
        public void Reconstruct_InnerJoin_MatchesOriginal()
        {
            var originalSql = "SELECT a.Col1 FROM TableA AS a INNER JOIN TableB AS b ON a.Id = b.Id;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_LeftOuterJoin_MatchesOriginal()
        {
            var originalSql = "SELECT a.Col FROM TableA a LEFT OUTER JOIN TableB b ON a.Id = b.Id;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }
        [TestMethod]
        public void Reconstruct_TableAlias_ShouldNotDuplicate()
        {
            var sql = "SELECT A.Col FROM TableA AS A;";
            var parsed = _parser.Parse(sql);
            var recon = _reconstructor.Reconstruct(parsed);
            Assert.AreEqual(NormalizeSqlForComparison("SELECT A.Col FROM TableA AS A;"), NormalizeSqlForComparison(recon));
        }
        [TestMethod]
        public void Reconstruct_GroupByHavingOrderBy_MatchesOriginal()
        {
            var originalSql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 5 ORDER BY Category DESC;";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_WhereInSubquery_MatchesOriginal()
        {
            // Original SQL now expects single parentheses for IN subquery
            var originalSql = "SELECT Id FROM Users WHERE Id IN (SELECT UserID FROM ActiveUsers);";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_WhereExistsSubquery_MatchesOriginal()
        {
            var originalSql = "SELECT Id FROM Users WHERE EXISTS (SELECT 1 FROM Orders WHERE Orders.UserId = Users.Id);";
            var parsedStatement = _parser.Parse(originalSql);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(originalSql), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_ComplexQuery_MatchesOriginal()
        {
            var sqlQuery = @"
                SELECT
                    u.UserID,
                    u.UserName,
                    o.OrderID,
                    (SELECT COUNT(li.ItemID) FROM OrderItems li WHERE li.OrderID = o.OrderID) AS TotalItems,
                    (
                        SELECT SUM(p.Price * li_sub.Quantity)
                        FROM OrderItems li_sub
                        INNER JOIN Products p ON li_sub.ProductID = p.ProductID
                        WHERE li_sub.OrderID = o.OrderID
                    ) AS TotalOrderValue
                FROM
                    Users AS u
                INNER JOIN
                    Orders AS o ON u.UserID = o.UserID
                WHERE
                    u.Status = 'Active'
                    AND o.OrderDate >= '2023-01-01'
                    AND o.TotalAmount > (SELECT AVG(sub_o.TotalAmount) FROM Orders sub_o WHERE sub_o.OrderDate = o.OrderDate)
                GROUP BY
                    u.UserID, u.UserName, o.OrderID
                HAVING
                    COUNT(o.OrderID) > 1
                ORDER BY
                    u.UserName ASC, o.OrderDate DESC;
            ";
            var parsedStatement = _parser.Parse(sqlQuery);
            var reconstructedSql = _reconstructor.Reconstruct(parsedStatement);
            Assert.AreEqual(NormalizeSqlForComparison(sqlQuery), NormalizeSqlForComparison(reconstructedSql));
        }

        [TestMethod]
        public void Reconstruct_InClauseWithSubqueryAndExcept_PreservesQuotedIdentifiersAndSyntax()
        {
            // Arrange
            var originalSql = @"
        SELECT 
            WH.""Well ID"", 
            WH.""Well Name"", 
            WH.""Well Number"", 
            WH.""Latitude"", 
            WH.""Longitude""
        FROM ""DBA"".""WellHeader"" AS WH
        WHERE WH.""Well ID"" IN (
            SELECT WH2.""Well ID""
            FROM ""DBA"".""WellHeader"" AS WH2
            EXCEPT
            SELECT WC.""Well ID""
            FROM ""WellCurves"" AS WC
            WHERE WC.""Curve Name"" IN ('CALI')
        );
    ";

            var statement = _parser.Parse(originalSql, SqlDialect.SqlAnywhere);
            var reconstructed = _reconstructor.Reconstruct(statement, SqlDialect.SqlAnywhere).Trim();

            const string expectedReconstruction =
@"SELECT WH.""Well ID"", WH.""Well Name"", WH.""Well Number"", WH.""Latitude"", WH.""Longitude"" FROM ""DBA"".""WellHeader"" AS WH WHERE WH.""Well ID"" IN (SELECT WH2.""Well ID"" FROM ""DBA"".""WellHeader"" AS WH2 EXCEPT SELECT WC.""Well ID"" FROM ""WellCurves"" AS WC WHERE WC.""Curve Name"" IN ('CALI'));";


            Assert.AreEqual(expectedReconstruction, reconstructed);
        }

        [TestMethod]
        public void RoundTrip_QuotedIdentifiers_WithSetOperators_Subquery()
        {
            string sql = @"
        SELECT
            WH.""Well ID"",
            WH.""Well Name"",
            WH.""Well Number"",
            WH.""Latitude"",
            WH.""Longitude""
        FROM ""DBA"".""WellHeader"" AS WH
        WHERE WH.""Well ID"" IN (
            SELECT WH2.""Well ID""
            FROM ""DBA"".""WellHeader"" AS WH2
            EXCEPT
            SELECT WC.""Well ID""
            FROM ""WellCurves"" AS WC
            WHERE WC.""Curve Name"" IN ('CALI')
        );";

            var ast = _parser.Parse(sql);

            var reconstructor = new SqlAnywhereReconstructor();
            var output = reconstructor.Reconstruct(ast);

            Assert.AreEqual(Normalize(sql), Normalize(output));
        }

        [TestMethod]
        public void RoundTrip_CreateTable_WithConstraints_IsStable()
        {
            var sql = "CREATE TABLE Users (Id INT IDENTITY(1,1) PRIMARY KEY, ParentId INT, Name VARCHAR(50) NOT NULL, " +
                      "CONSTRAINT FK_Users_Parent FOREIGN KEY (ParentId) REFERENCES Users(Id) ON DELETE CASCADE, " +
                      "CHECK (Id > 0));";
            var ast1 = _parser.Parse(sql);
            var recon1 = _reconstructor.Reconstruct(ast1, SqlDialect.SqlAnywhere);
            var ast2 = _parser.Parse(recon1, SqlDialect.SqlAnywhere);
            var recon2 = _reconstructor.Reconstruct(ast2, SqlDialect.SqlAnywhere);

            Assert.AreEqual(Normalize(recon1), Normalize(recon2));
        }

        [TestMethod]
        public void RoundTrip_CreateIndex_IsStable()
        {
            var sql = "CREATE UNIQUE INDEX IX_Users_Name ON Users (Name DESC);";
            var ast1 = _parser.Parse(sql);
            var recon1 = _reconstructor.Reconstruct(ast1, SqlDialect.SqlAnywhere);
            var ast2 = _parser.Parse(recon1, SqlDialect.SqlAnywhere);
            var recon2 = _reconstructor.Reconstruct(ast2, SqlDialect.SqlAnywhere);

            Assert.AreEqual(Normalize(recon1), Normalize(recon2));
        }
    }
}