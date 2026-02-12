using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Utils;

namespace SqlForge.Tests
{
    [TestClass]
    public class DialectKeywordRegistryTests
    {
        [TestMethod]
        public void Tokenizer_MsSqlServer_Merge_IsKeyword()
        {
            var tokens = new Tokenizer("MERGE", SqlDialect.MsSqlServer).Tokenize();

            Assert.AreEqual(TokenType.Keyword, tokens[0].Type);
            Assert.AreEqual("MERGE", tokens[0].Value);
        }

        [TestMethod]
        public void Tokenizer_SqlAnywhere_Merge_IsIdentifier()
        {
            var tokens = new Tokenizer("MERGE", SqlDialect.SqlAnywhere).Tokenize();

            Assert.AreEqual(TokenType.Identifier, tokens[0].Type);
            Assert.AreEqual("MERGE", tokens[0].Value);
        }

        [TestMethod]
        public void Tokenizer_MySql_XorAndRegexp_AreKeywords()
        {
            var tokens = new Tokenizer("XOR REGEXP", SqlDialect.MySql).Tokenize();

            Assert.AreEqual(TokenType.Keyword, tokens[0].Type);
            Assert.AreEqual("XOR", tokens[0].Value);
            Assert.AreEqual(TokenType.Keyword, tokens[1].Type);
            Assert.AreEqual("REGEXP", tokens[1].Value);
        }

        [TestMethod]
        public void SqlAnywhereParser_AllowsMerge_AsUnquotedIdentifier()
        {
            ISqlParser parser = SqlForgeFactory.CreateParser(SqlDialect.SqlAnywhere);

            var ast = parser.Parse("SELECT MERGE FROM users;");
            var select = ast.Body as SelectStatement;
            var column = select?.SelectItems[0].Expression as ColumnExpression;

            Assert.IsNotNull(select);
            Assert.IsNotNull(column);
            Assert.AreEqual("MERGE", column.ColumnName);
        }

        [TestMethod]
        public void MsSqlServerParser_Merge_AsBareIdentifier_Throws()
        {
            ISqlParser parser = SqlForgeFactory.CreateParser(SqlDialect.MsSqlServer);

            Assert.ThrowsException<SqlParseException>(() => parser.Parse("SELECT MERGE FROM users;"));
        }
    }
}
