using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Parsers;

namespace SqlForge.Tests
{
    [TestClass]
    public class MySqlParserTests
    {
        private ISqlParser _parser;

        [TestInitialize]
        public void Setup()
        {
            var factoryHolder = new StatementParserFactoryHolder();
            IExpressionParser expressionParser = new MySqlExpressionParser(factoryHolder);
            var selectParser = new MySqlSelectStatementParser(expressionParser, factoryHolder);
            IStatementParserFactory factory = new MySqlStatementParserFactory(selectParser, expressionParser);
            factoryHolder.SetActualFactory(factory);
            _parser = new MySqlParser(factory);
        }

        [TestMethod]
        public void Parse_LimitCount_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users LIMIT 10;");
            var select = (SelectStatement)ast.Body;

            Assert.IsNotNull(select.LimitClause);
            Assert.AreEqual("10", ((LiteralExpression)select.LimitClause.CountExpression).Value);
        }

        [TestMethod]
        public void Parse_LimitOffsetSyntax_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users LIMIT 5 OFFSET 10;");
            var select = (SelectStatement)ast.Body;

            Assert.IsNotNull(select.LimitClause);
            Assert.AreEqual("5", ((LiteralExpression)select.LimitClause.CountExpression).Value);
            Assert.AreEqual("10", ((LiteralExpression)select.LimitClause.OffsetExpression).Value);
        }

        [TestMethod]
        public void Parse_LimitCommaSyntax_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users LIMIT 10, 5;");
            var select = (SelectStatement)ast.Body;

            Assert.IsNotNull(select.LimitClause);
            Assert.AreEqual("10", ((LiteralExpression)select.LimitClause.OffsetExpression).Value);
            Assert.AreEqual("5", ((LiteralExpression)select.LimitClause.CountExpression).Value);
        }

        [TestMethod]
        public void Parse_BacktickIdentifiers_PreservesQuoteStyles()
        {
            var ast = _parser.Parse("SELECT `User ID` FROM `Users`;");
            var select = (SelectStatement)ast.Body;
            var col = (ColumnExpression)select.SelectItems[0].Expression;
            var table = (TableExpression)select.FromClause.TableExpressions[0];

            Assert.AreEqual(QuoteStyle.Backtick, col.ColumnQuoteStyle);
            Assert.AreEqual(QuoteStyle.Backtick, table.TableQuoteStyle);
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
        public void Parse_UnionAll_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM a UNION ALL SELECT id FROM b;");
            var setExpr = (SetOperatorExpression)ast.Body;

            Assert.AreEqual(SetOperatorType.UnionAll, setExpr.Operator);
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
        public void Parse_WindowFunction_WithAggregateOver_ParsesAsWindowFunction()
        {
            var ast = _parser.Parse("SELECT SUM(amount) OVER (ORDER BY id ROWS BETWEEN 1 PRECEDING AND CURRENT ROW) FROM orders;");
            var select = (SelectStatement)ast.Body;
            var window = (WindowFunctionExpression)select.SelectItems[0].Expression;

            Assert.AreEqual("SUM", window.FunctionName);
            Assert.IsNotNull(window.Frame);
            Assert.AreEqual(WindowFrameType.Rows, window.Frame.Type);
        }

        [TestMethod]
        public void Parse_FunctionCall_IfNull_Succeeds()
        {
            var ast = _parser.Parse("SELECT IFNULL(name, 'n/a') FROM users;");
            var select = (SelectStatement)ast.Body;
            var func = (FunctionCallExpression)select.SelectItems[0].Expression;

            Assert.AreEqual("IFNULL", func.FunctionName);
            Assert.AreEqual(2, func.Arguments.Count);
        }

        [TestMethod]
        public void Parse_FunctionCall_Now_Succeeds()
        {
            var ast = _parser.Parse("SELECT NOW() FROM users;");
            var select = (SelectStatement)ast.Body;
            var func = (FunctionCallExpression)select.SelectItems[0].Expression;

            Assert.AreEqual("NOW", func.FunctionName);
            Assert.AreEqual(0, func.Arguments.Count);
        }

        [TestMethod]
        public void Parse_NotInExpression_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users WHERE id NOT IN (1, 2, 3);");
            var select = (SelectStatement)ast.Body;
            var where = (WhereClause)select.WhereClause;
            var inExpr = (InExpression)where.Condition;

            Assert.IsTrue(inExpr.IsNegated);
            Assert.AreEqual(3, inExpr.Values.Count);
        }

        [TestMethod]
        public void Parse_CrossJoin_Succeeds()
        {
            var ast = _parser.Parse("SELECT a.id FROM a CROSS JOIN b;");
            var select = (SelectStatement)ast.Body;
            var join = (JoinExpression)select.FromClause.TableExpressions[0];

            Assert.AreEqual(JoinType.Cross, join.Type);
            Assert.IsNull(join.OnCondition);
        }

        [TestMethod]
        public void Parse_NullSafeEqual_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users WHERE name <=> 'a';");
            var select = (SelectStatement)ast.Body;
            var where = select.WhereClause;
            var bin = (BinaryExpression)where.Condition;

            Assert.AreEqual("<=>", bin.Operator);
        }

        [TestMethod]
        public void Parse_Between_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users WHERE age BETWEEN 18 AND 65;");
            var select = (SelectStatement)ast.Body;
            var between = (BetweenExpression)select.WhereClause.Condition;

            Assert.IsFalse(between.IsNegated);
        }

        [TestMethod]
        public void Parse_NotLike_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users WHERE name NOT LIKE 'a%';");
            var select = (SelectStatement)ast.Body;
            var bin = (BinaryExpression)select.WhereClause.Condition;

            Assert.AreEqual("NOT LIKE", bin.Operator);
        }

        [TestMethod]
        public void Parse_Regexp_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users WHERE name REGEXP '^[a]';");
            var select = (SelectStatement)ast.Body;
            var bin = (BinaryExpression)select.WhereClause.Condition;

            Assert.AreEqual("REGEXP", bin.Operator);
        }

        [TestMethod]
        public void Parse_IsTrue_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users WHERE active IS TRUE;");
            var select = (SelectStatement)ast.Body;
            var bin = (BinaryExpression)select.WhereClause.Condition;

            Assert.AreEqual("IS", bin.Operator);
        }

        [TestMethod]
        public void Parse_Xor_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users WHERE a = 1 XOR b = 2;");
            var select = (SelectStatement)ast.Body;
            var bin = (BinaryExpression)select.WhereClause.Condition;

            Assert.AreEqual("XOR", bin.Operator);
        }

        [TestMethod]
        public void Parse_Div_Mod_Succeeds()
        {
            var ast = _parser.Parse("SELECT 10 DIV 3, 10 MOD 3 FROM dual;");
            var select = (SelectStatement)ast.Body;
            var divExpr = (BinaryExpression)select.SelectItems[0].Expression;
            var modExpr = (BinaryExpression)select.SelectItems[1].Expression;

            Assert.AreEqual("DIV", divExpr.Operator);
            Assert.AreEqual("MOD", modExpr.Operator);
        }

        [TestMethod]
        public void Parse_SelectModifiers_Succeeds()
        {
            var ast = _parser.Parse("SELECT SQL_CALC_FOUND_ROWS HIGH_PRIORITY id FROM users;");
            var select = (SelectStatement)ast.Body;

            Assert.IsTrue(select.SelectModifiers.Contains("SQL_CALC_FOUND_ROWS"));
            Assert.IsTrue(select.SelectModifiers.Contains("HIGH_PRIORITY"));
        }

        [TestMethod]
        public void Parse_CreateTable_ForeignKeyAndCheck_Succeeds()
        {
            var sql = "CREATE TABLE users (id INT PRIMARY KEY, parent_id INT, " +
                      "CONSTRAINT fk_users_parent FOREIGN KEY (parent_id) REFERENCES users(id) ON DELETE SET NULL, " +
                      "CHECK (id > 0));";
            var ast = _parser.Parse(sql);
            var create = (CreateTableStatement)ast.Body;

            Assert.AreEqual(2, create.Constraints.Count);
        }

        [TestMethod]
        public void Parse_CreateIndex_Succeeds()
        {
            var sql = "CREATE INDEX idx_users_name ON users (name);";
            var ast = _parser.Parse(sql);

            Assert.AreEqual(StatementType.CreateIndex, ast.Type);
            var createIndex = (CreateIndexStatement)ast.Body;
            Assert.AreEqual(IndexType.Index, createIndex.Index.Type);
            Assert.AreEqual("idx_users_name", createIndex.Index.Name);
        }

        [TestMethod]
        public void Parse_SelectIntoOutfile_Succeeds()
        {
            var ast = _parser.Parse("SELECT id INTO OUTFILE 'out.txt' FROM users;");
            var select = (SelectStatement)ast.Body;

            Assert.IsNotNull(select.IntoClause);
            Assert.AreEqual(SelectIntoType.Outfile, select.IntoClause.Type);
            Assert.AreEqual("out.txt", select.IntoClause.FilePath);
        }

        [TestMethod]
        public void Parse_GroupByWithRollup_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users GROUP BY id WITH ROLLUP;");
            var select = (SelectStatement)ast.Body;

            Assert.IsTrue(select.GroupByClause.WithRollup);
        }

        [TestMethod]
        public void Parse_ForUpdate_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users FOR UPDATE;");
            var select = (SelectStatement)ast.Body;

            Assert.IsTrue(select.ForUpdate);
        }

        [TestMethod]
        public void Parse_LockInShareMode_Succeeds()
        {
            var ast = _parser.Parse("SELECT id FROM users LOCK IN SHARE MODE;");
            var select = (SelectStatement)ast.Body;

            Assert.IsTrue(select.LockInShareMode);
        }

        [TestMethod]
        public void Parse_InsertValues_Succeeds()
        {
            var ast = _parser.Parse("INSERT INTO users (id, name) VALUES (1, 'a');");
            var insert = (InsertStatement)ast.Body;

            Assert.AreEqual(2, insert.Columns.Count);
            Assert.AreEqual(1, insert.Values.Count);
        }

        [TestMethod]
        public void Parse_Replace_Succeeds()
        {
            var ast = _parser.Parse("REPLACE INTO users (id) VALUES (1);");
            var insert = (InsertStatement)ast.Body;

            Assert.IsTrue(insert.IsReplace);
        }

        [TestMethod]
        public void Parse_Update_Succeeds()
        {
            var ast = _parser.Parse("UPDATE users SET name = 'a' WHERE id = 1 LIMIT 1;");
            var update = (UpdateStatement)ast.Body;

            Assert.AreEqual(1, update.SetClauses.Count);
            Assert.IsNotNull(update.WhereClause);
            Assert.IsNotNull(update.LimitClause);
        }

        [TestMethod]
        public void Parse_Delete_Succeeds()
        {
            var ast = _parser.Parse("DELETE FROM users WHERE id = 1 ORDER BY id LIMIT 1;");
            var delete = (DeleteStatement)ast.Body;

            Assert.IsNotNull(delete.WhereClause);
            Assert.IsNotNull(delete.OrderByClause);
            Assert.IsNotNull(delete.LimitClause);
        }

        [TestMethod]
        public void Parse_CreateTable_Succeeds()
        {
            var ast = _parser.Parse("CREATE TABLE users (id INT NOT NULL, name VARCHAR(20));");
            var create = (CreateTableStatement)ast.Body;

            Assert.AreEqual(2, create.Columns.Count);
        }

        [TestMethod]
        public void Parse_DropTable_Succeeds()
        {
            var ast = _parser.Parse("DROP TABLE IF EXISTS users, logs;");
            var drop = (DropTableStatement)ast.Body;

            Assert.IsTrue(drop.IfExists);
            Assert.AreEqual(2, drop.Targets.Count);
        }

        [TestMethod]
        public void Parse_AlterTable_Succeeds()
        {
            var ast = _parser.Parse("ALTER TABLE users ADD COLUMN age INT, DROP COLUMN name;");
            var alter = (AlterTableStatement)ast.Body;

            Assert.AreEqual(2, alter.Actions.Count);
        }

        [TestMethod]
        public void Parse_CreateTable_WithIndexesAndOptions_Succeeds()
        {
            var sql = "CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(20), KEY idx_name (name(10)) ) ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            var ast = _parser.Parse(sql);
            var create = (CreateTableStatement)ast.Body;

            Assert.AreEqual(2, create.Columns.Count);
            Assert.AreEqual(1, create.Indexes.Count);
            Assert.IsTrue(create.TableOptions.Count >= 2);
        }

        [TestMethod]
        public void Parse_AlterTable_ChangeRenameIndex_Succeeds()
        {
            var sql = "ALTER TABLE users CHANGE COLUMN name full_name VARCHAR(50), RENAME TO users_new, ADD INDEX idx_name (full_name), DROP INDEX idx_old;";
            var ast = _parser.Parse(sql);
            var alter = (AlterTableStatement)ast.Body;

            Assert.AreEqual(4, alter.Actions.Count);
        }
    }
}
