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
    }
}
