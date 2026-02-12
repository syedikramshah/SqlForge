using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Reconstructors;

namespace SqlForge.Tests
{
    [TestClass]
    public class SqlForgeFactoryTests
    {
        [TestMethod]
        public void Factory_CreatesMsSqlComponents()
        {
            var parser = SqlForgeFactory.CreateParser(SqlDialect.MsSqlServer);
            var reconstructor = SqlForgeFactory.CreateReconstructor(SqlDialect.MsSqlServer);
            var formatter = SqlForgeFactory.CreateFormatter(SqlDialect.MsSqlServer);

            Assert.IsNotNull(parser);
            Assert.IsNotNull(reconstructor);
            Assert.IsNotNull(formatter);
            Assert.IsInstanceOfType(reconstructor, typeof(MsSqlServerReconstructor));
        }

        [TestMethod]
        public void Factory_MsSqlParser_ParsesBasicSelect()
        {
            ISqlParser parser = SqlForgeFactory.CreateParser(SqlDialect.MsSqlServer);
            var ast = parser.Parse("SELECT TOP 1 id FROM users;");
            Assert.AreEqual(StatementType.Select, ast.Type);
            Assert.IsInstanceOfType(ast.Body, typeof(SelectStatement));
        }

        [TestMethod]
        public void Factory_CreatesMySqlComponents()
        {
            var parser = SqlForgeFactory.CreateParser(SqlDialect.MySql);
            var reconstructor = SqlForgeFactory.CreateReconstructor(SqlDialect.MySql);
            var formatter = SqlForgeFactory.CreateFormatter(SqlDialect.MySql);

            Assert.IsNotNull(parser);
            Assert.IsNotNull(reconstructor);
            Assert.IsNotNull(formatter);
            Assert.IsInstanceOfType(reconstructor, typeof(MySqlReconstructor));
        }

        [TestMethod]
        public void Factory_MySqlParser_ParsesBasicSelect()
        {
            ISqlParser parser = SqlForgeFactory.CreateParser(SqlDialect.MySql);
            var ast = parser.Parse("SELECT id FROM users LIMIT 1;");
            Assert.AreEqual(StatementType.Select, ast.Type);
            Assert.IsInstanceOfType(ast.Body, typeof(SelectStatement));
        }
    }
}
