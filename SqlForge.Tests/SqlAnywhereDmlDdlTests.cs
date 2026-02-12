using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Interfaces;
using SqlForge;
using SqlForge.Enums;
using SqlForge.Nodes;
using SqlForge.Reconstructors;
using System.Text.RegularExpressions;

namespace SqlForge.Tests
{
    [TestClass]
    public class SqlAnywhereDmlDdlTests
    {
        private ISqlParser _parser;
        private ISqlReconstructor _reconstructor;

        [TestInitialize]
        public void Setup()
        {
            _parser = SqlForgeFactory.CreateParser(SqlDialect.SqlAnywhere);
            _reconstructor = SqlForgeFactory.CreateReconstructor(SqlDialect.SqlAnywhere);
        }

        [TestMethod]
        public void Parse_InsertValues_ParsesColumnsAndValues()
        {
            var sql = "INSERT INTO Users (Id, Name) VALUES (1, 'Ada');";
            var ast = _parser.Parse(sql);

            Assert.AreEqual(StatementType.Insert, ast.Type);
            var insert = (InsertStatement)ast.Body;
            Assert.AreEqual("Users", insert.Target.TableName);
            Assert.AreEqual(2, insert.Columns.Count);
            Assert.AreEqual(1, insert.Values.Count);
            Assert.AreEqual(2, insert.Values[0].Count);
        }

        [TestMethod]
        public void Parse_UpdateDelete_ParsesWhereClauses()
        {
            var updateSql = "UPDATE Users SET Name = 'Ada' WHERE Id = 1;";
            var updateAst = _parser.Parse(updateSql);
            Assert.AreEqual(StatementType.Update, updateAst.Type);
            var update = (UpdateStatement)updateAst.Body;
            Assert.AreEqual(1, update.SetClauses.Count);
            Assert.IsNotNull(update.WhereClause);

            var deleteSql = "DELETE FROM Users WHERE Id = 1;";
            var deleteAst = _parser.Parse(deleteSql);
            Assert.AreEqual(StatementType.Delete, deleteAst.Type);
            var delete = (DeleteStatement)deleteAst.Body;
            Assert.IsNotNull(delete.WhereClause);
        }

        [TestMethod]
        public void Parse_CreateTable_IdentityAndConstraints()
        {
            var sql = "CREATE TABLE Users (Id INT IDENTITY(1,1) PRIMARY KEY, ParentId INT, Name VARCHAR(50) NOT NULL, " +
                      "CONSTRAINT FK_Users_Parent FOREIGN KEY (ParentId) REFERENCES Users(Id), CHECK (Id > 0), UNIQUE (Name));";
            var ast = _parser.Parse(sql);
            Assert.AreEqual(StatementType.Create, ast.Type);
            var create = (CreateTableStatement)ast.Body;

            Assert.AreEqual(3, create.Columns.Count);
            Assert.IsTrue(create.Columns[0].IsIdentity);
            Assert.IsTrue(create.Columns[0].IsPrimaryKey);
            Assert.IsNotNull(create.Columns[0].IdentitySeed);
            Assert.IsNotNull(create.Columns[0].IdentityIncrement);
            Assert.AreEqual(3, create.Constraints.Count);
        }

        [TestMethod]
        public void Parse_CreateIndex_Succeeds()
        {
            var sql = "CREATE UNIQUE INDEX IX_Users_Name ON Users (Name DESC);";
            var ast = _parser.Parse(sql);

            Assert.AreEqual(StatementType.CreateIndex, ast.Type);
            var createIndex = (CreateIndexStatement)ast.Body;
            Assert.AreEqual(IndexType.Unique, createIndex.Index.Type);
            Assert.AreEqual("IX_Users_Name", createIndex.Index.Name);
            Assert.AreEqual("Users", createIndex.Target.TableName);
            Assert.AreEqual(1, createIndex.Index.Columns.Count);
        }

        [TestMethod]
        public void Parse_AlterTable_MultipleActions()
        {
            var sql = "ALTER TABLE Users ADD COLUMN Age INT, DROP COLUMN OldCol, ALTER COLUMN Name VARCHAR(100), RENAME COLUMN Name TO FullName;";
            var ast = _parser.Parse(sql);
            Assert.AreEqual(StatementType.Alter, ast.Type);
            var alter = (AlterTableStatement)ast.Body;
            Assert.AreEqual(4, alter.Actions.Count);
            Assert.AreEqual(AlterTableActionType.AddColumn, alter.Actions[0].ActionType);
            Assert.AreEqual(AlterTableActionType.DropColumn, alter.Actions[1].ActionType);
            Assert.AreEqual(AlterTableActionType.AlterColumn, alter.Actions[2].ActionType);
            Assert.AreEqual(AlterTableActionType.RenameColumn, alter.Actions[3].ActionType);
        }

        [TestMethod]
        public void Reconstruct_CreateTable_RoundTrips()
        {
            var sql = "CREATE TABLE Users (Id INT IDENTITY PRIMARY KEY, Name VARCHAR(50) NOT NULL);";
            var ast = _parser.Parse(sql);
            var reconstructed = _reconstructor.Reconstruct(ast, SqlDialect.SqlAnywhere);
            Assert.AreEqual(Normalize(sql), Normalize(reconstructed));
        }

        private static string Normalize(string sql)
        {
            sql = Regex.Replace(sql, @"\s+", " ").Trim();
            sql = Regex.Replace(sql, @"\s*([(),=])\s*", "$1");
            return sql.Trim().TrimEnd(';').ToUpperInvariant();
        }
    }
}
