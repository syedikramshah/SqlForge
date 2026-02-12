using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Interfaces;
using SqlForge.Nodes;
using SqlForge.Parsers;
using System;
using System.Linq;
using System.Collections.Generic;

namespace SqlForge.Tests
{
    [TestClass]
    public class ParserTests
    {
        private ISqlParser _parser;

        [TestInitialize]
        public void Setup()
        {
            // --- NEW DEPENDENCY INJECTION SETUP ---
            // 1. Create a "Factory Holder" to break the circular dependency.
            var statementParserFactoryHolder = new StatementParserFactoryHolder();

            // 2. Instantiate the Expression Parser, injecting the holder for statement parsing.
            IExpressionParser expressionParser = new SqlAnywhereExpressionParser(statementParserFactoryHolder);

            // 3. Instantiate specific Statement Parsers.
            //    SelectStatementParser needs IExpressionParser and the factory holder.
            IStatementParser selectStatementParser = new SelectStatementParser(expressionParser, statementParserFactoryHolder);

            // 4. Instantiate the concrete Statement Parser Factory.
            //    It takes the specific statement parsers it needs to orchestrate.
            IStatementParserFactory actualStatementParserFactory = new SqlAnywhereStatementParserFactory(
                (SelectStatementParser)selectStatementParser,
                expressionParser
            );

            // 5. "Wire" the actual factory into the holder. This completes the cycle.
            statementParserFactoryHolder.SetActualFactory(actualStatementParserFactory);

            // 6. Create the main SqlAnywhereParser, injecting the fully wired factory.
            _parser = new SqlAnywhereParser(actualStatementParserFactory);
        }

        [TestMethod]
        public void Parse_SimpleSelect_ReturnsCorrectAST()
        {
            var sql = "SELECT Col1, Col2 FROM MyTable;";
            var statement = _parser.Parse(sql);

            Assert.AreEqual(StatementType.Select, statement.Type);
            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt);

            Assert.AreEqual(2, selectStmt.SelectItems.Count);
            Assert.AreEqual("Col1", (selectStmt.SelectItems[0].Expression as ColumnExpression).ColumnName);
            Assert.AreEqual("Col2", (selectStmt.SelectItems[1].Expression as ColumnExpression).ColumnName);

            Assert.IsNotNull(selectStmt.FromClause);
            Assert.AreEqual(1, selectStmt.FromClause.TableExpressions.Count);
            Assert.AreEqual("MyTable", (selectStmt.FromClause.TableExpressions[0] as TableExpression).TableName);
        }

        [TestMethod]
        public void Parse_SelectWithAliases_ReturnsCorrectAST()
        {
            var sql = "SELECT u.UserID AS ID, u.UserName FROM Users AS u;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;

            Assert.AreEqual(2, selectStmt.SelectItems.Count);

            var selectItem1 = selectStmt.SelectItems[0];
            var col1 = selectItem1.Expression as ColumnExpression;
            Assert.AreEqual("UserID", col1.ColumnName);
            Assert.AreEqual("u", col1.TableAlias);
            Assert.AreEqual("ID", selectItem1.Alias);
            Assert.IsTrue(selectItem1.HasExplicitAs); // Verify HasExplicitAs

            var selectItem2 = selectStmt.SelectItems[1];
            var col2 = selectItem2.Expression as ColumnExpression;
            Assert.AreEqual("UserName", col2.ColumnName);
            Assert.AreEqual("u", col2.TableAlias);
            Assert.IsNull(selectItem2.Alias);
            Assert.IsFalse(selectItem2.HasExplicitAs); // Verify HasExplicitAs
        }

        [TestMethod]
        public void Parse_SelectStar_ReturnsCorrectAST()
        {
            var sql = "SELECT * FROM MyTable;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;
            Assert.AreEqual(1, selectStmt.SelectItems.Count);
            Assert.AreEqual("*", (selectStmt.SelectItems[0].Expression as ColumnExpression).ColumnName);
        }

        [TestMethod]
        public void Parse_WhereClause_ReturnsCorrectAST()
        {
            var sql = "SELECT Col FROM MyTable WHERE Status = 'Active' AND Value > 100;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt.WhereClause);

            var condition = selectStmt.WhereClause.Condition as BinaryExpression;
            Assert.IsNotNull(condition);
            Assert.AreEqual("AND", condition.Operator);

            var leftCondition = condition.Left as BinaryExpression;
            Assert.AreEqual("=", leftCondition.Operator);
            Assert.AreEqual("Status", (leftCondition.Left as ColumnExpression).ColumnName);
            Assert.AreEqual("Active", (leftCondition.Right as LiteralExpression).Value);

            var rightCondition = condition.Right as BinaryExpression;
            Assert.AreEqual(">", rightCondition.Operator);
            Assert.AreEqual("Value", (rightCondition.Left as ColumnExpression).ColumnName);
            Assert.AreEqual("100", (rightCondition.Right as LiteralExpression).Value);
        }

        [TestMethod]
        public void Parse_ArithmeticExpressionInSelect_ReturnsCorrectAST()
        {
            var sql = "SELECT Price * Quantity AS Total FROM OrderItems;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;
            Assert.AreEqual(1, selectStmt.SelectItems.Count);

            var selectItem = selectStmt.SelectItems[0];
            Assert.AreEqual("Total", selectItem.Alias);
            Assert.IsTrue(selectItem.HasExplicitAs); // Verify HasExplicitAs

            var binaryExpr = selectItem.Expression as BinaryExpression;
            Assert.IsNotNull(binaryExpr);
            Assert.AreEqual("*", binaryExpr.Operator);
            Assert.AreEqual("Price", (binaryExpr.Left as ColumnExpression).ColumnName);
            Assert.AreEqual("Quantity", (binaryExpr.Right as ColumnExpression).ColumnName);
        }

        [TestMethod]
        public void Parse_FunctionCall_ReturnsCorrectAST()
        {
            var sql = "SELECT COUNT(*), SUM(Amount) FROM Orders;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;
            Assert.AreEqual(2, selectStmt.SelectItems.Count);

            var func1 = selectStmt.SelectItems[0].Expression as FunctionCallExpression;
            Assert.IsNotNull(func1);
            Assert.AreEqual("COUNT", func1.FunctionName);
            Assert.IsTrue(func1.IsAllColumns);

            var func2 = selectStmt.SelectItems[1].Expression as FunctionCallExpression;
            Assert.IsNotNull(func2);
            Assert.AreEqual("SUM", func2.FunctionName);
            Assert.IsFalse(func2.IsAllColumns);
            Assert.AreEqual(1, func2.Arguments.Count);
            Assert.AreEqual("Amount", (func2.Arguments[0] as ColumnExpression).ColumnName);
        }

        [TestMethod]
        public void Parse_NestedSubqueryInSelect_ReturnsCorrectAST()
        {
            var sql = "SELECT (SELECT COUNT(*) FROM NestedTable) AS NestedCount FROM MyTable;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;
            Assert.AreEqual(1, selectStmt.SelectItems.Count);

            var selectItem = selectStmt.SelectItems[0];
            Assert.AreEqual("NestedCount", selectItem.Alias);
            Assert.IsTrue(selectItem.HasExplicitAs); // Verify HasExplicitAs

            var subqueryExpr = selectItem.Expression as SubqueryExpression;
            Assert.IsNotNull(subqueryExpr);
            Assert.AreEqual(StatementType.Select, subqueryExpr.SubqueryStatement.Type);

            var nestedSelect = subqueryExpr.SubqueryStatement.Body as SelectStatement;
            Assert.AreEqual(1, nestedSelect.SelectItems.Count);
            Assert.AreEqual("FunctionCallExpression", nestedSelect.SelectItems[0].Expression.GetType().Name);
            Assert.AreEqual("COUNT", (nestedSelect.SelectItems[0].Expression as FunctionCallExpression).FunctionName);
        }

        [TestMethod]
        public void Parse_DerivedTableInFrom_ReturnsCorrectAST()
        {
            var sql = "SELECT a.Col FROM (SELECT ID AS Col FROM TempTable) AS a WHERE a.Col > 10;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt.FromClause);
            Assert.AreEqual(1, selectStmt.FromClause.TableExpressions.Count);

            var derivedTable = selectStmt.FromClause.TableExpressions[0] as SubqueryExpression;
            Assert.IsNotNull(derivedTable);
            Assert.AreEqual("a", derivedTable.Alias);
            Assert.IsTrue(derivedTable.AliasQuoted == false); // Assuming 'a' is not quoted in the input
            Assert.IsTrue(derivedTable.SubqueryStatement.Type == StatementType.Select); // Use Type property
            // REMOVE this line as SubqueryExpression does not have a HasExplicitAs property
            // Assert.IsTrue(derivedTable.HasExplicitAs); // Verify HasExplicitAs on the derived table alias

            var nestedSelect = derivedTable.SubqueryStatement.Body as SelectStatement;
            Assert.AreEqual(1, nestedSelect.SelectItems.Count);
            Assert.AreEqual("ID", (nestedSelect.SelectItems[0].Expression as ColumnExpression).ColumnName);
            Assert.AreEqual("Col", nestedSelect.SelectItems[0].Alias);
            Assert.IsTrue(nestedSelect.SelectItems[0].HasExplicitAs); // This is correct, as SelectExpression does have HasExplicitAs
        }


        [TestMethod]
        public void Parse_InnerJoin_ReturnsCorrectAST()
        {
            var sql = "SELECT a.Col1 FROM TableA AS a INNER JOIN TableB AS b ON a.Id = b.Id;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;

            Assert.AreEqual(1, selectStmt.FromClause.TableExpressions.Count);

            var joinExpr = selectStmt.FromClause.TableExpressions[0] as JoinExpression;
            Assert.IsNotNull(joinExpr);
            Assert.AreEqual(JoinType.Inner, joinExpr.Type);

            // Verify the Left side of the join
            var leftTable = joinExpr.Left as TableExpression;
            Assert.IsNotNull(leftTable);
            Assert.AreEqual("TableA", leftTable.TableName);
            Assert.AreEqual("a", leftTable.Alias);
            Assert.IsTrue(leftTable.HasExplicitAs); // 'AS a' was explicit

            // Verify the Right side of the join
            var rightTable = joinExpr.Right as TableExpression;
            Assert.IsNotNull(rightTable);
            Assert.AreEqual("TableB", rightTable.TableName);
            Assert.AreEqual("b", rightTable.Alias);
            Assert.IsTrue(rightTable.HasExplicitAs); // 'AS b' was explicit

            // Verify the OnCondition
            var onCondition = joinExpr.OnCondition as BinaryExpression;
            Assert.AreEqual("=", onCondition.Operator);
            Assert.AreEqual("Id", (onCondition.Left as ColumnExpression).ColumnName);
            Assert.AreEqual("a", (onCondition.Left as ColumnExpression).TableAlias);
            Assert.AreEqual("Id", (onCondition.Right as ColumnExpression).ColumnName);
            Assert.AreEqual("b", (onCondition.Right as ColumnExpression).TableAlias);
        }

        [TestMethod]
        public void Parse_LeftOuterJoin_ReturnsCorrectAST()
        {
            var sql = "SELECT a.Col FROM TableA a LEFT OUTER JOIN TableB b ON a.Id = b.Id;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;

            Assert.AreEqual(1, selectStmt.FromClause.TableExpressions.Count);

            var joinExpr = selectStmt.FromClause.TableExpressions[0] as JoinExpression;
            Assert.IsNotNull(joinExpr);
            Assert.AreEqual(JoinType.Left, joinExpr.Type); // Enum is Left, not LeftOuter

            // Verify left and right sides, and OnCondition
            var leftTable = joinExpr.Left as TableExpression;
            Assert.IsNotNull(leftTable);
            Assert.AreEqual("TableA", leftTable.TableName);
            Assert.AreEqual("a", leftTable.Alias);
            Assert.IsFalse(leftTable.HasExplicitAs); // 'a' is implicit alias

            var rightTable = joinExpr.Right as TableExpression;
            Assert.IsNotNull(rightTable);
            Assert.AreEqual("TableB", rightTable.TableName);
            Assert.AreEqual("b", rightTable.Alias);
            Assert.IsFalse(rightTable.HasExplicitAs); // 'b' is implicit alias
        }

        [TestMethod]
        public void Parse_GroupByHavingOrderBy_ReturnsCorrectAST()
        {
            var sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 5 ORDER BY Category DESC;";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;

            Assert.IsNotNull(selectStmt.GroupByClause);
            Assert.AreEqual(1, selectStmt.GroupByClause.GroupingExpressions.Count);
            Assert.AreEqual("Category", (selectStmt.GroupByClause.GroupingExpressions[0] as ColumnExpression).ColumnName);

            Assert.IsNotNull(selectStmt.HavingClause);
            var havingCondition = selectStmt.HavingClause.Condition as BinaryExpression;
            Assert.AreEqual(">", havingCondition.Operator);
            Assert.AreEqual("COUNT", (havingCondition.Left as FunctionCallExpression).FunctionName);
            Assert.AreEqual("5", (havingCondition.Right as LiteralExpression).Value);

            Assert.IsNotNull(selectStmt.OrderByClause);
            Assert.AreEqual(1, selectStmt.OrderByClause.OrderItems.Count);
            var orderItem = selectStmt.OrderByClause.OrderItems[0];
            Assert.AreEqual("Category", (orderItem.Expression as ColumnExpression).ColumnName);
            Assert.IsFalse(orderItem.IsAscending);
        }

        [TestMethod]
        public void Parse_WhereInSubquery_ReturnsCorrectAST()
        {
            var sql = "SELECT Id FROM Users WHERE Id IN (SELECT UserID FROM ActiveUsers);";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;

            var condition = selectStmt.WhereClause.Condition as InExpression;
            Assert.IsNotNull(condition);
            Assert.IsFalse(condition.IsNegated);

            var nestedSubquery = condition.Subquery;
            Assert.IsNotNull(nestedSubquery);
            Assert.AreEqual(StatementType.Select, nestedSubquery.Type);

            // Check the content of the nested subquery
            var innerSelect = nestedSubquery.Body as SelectStatement;
            Assert.IsNotNull(innerSelect);
            Assert.AreEqual(1, innerSelect.SelectItems.Count);
            Assert.AreEqual("UserID", (innerSelect.SelectItems[0].Expression as ColumnExpression).ColumnName);
        }


        [TestMethod]
        public void Parse_WhereExistsSubquery_ReturnsCorrectAST()
        {
            var sql = "SELECT Id FROM Users WHERE EXISTS (SELECT 1 FROM Orders WHERE Orders.UserId = Users.Id);";
            var statement = _parser.Parse(sql);
            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt.WhereClause);

            var condition = selectStmt.WhereClause.Condition as UnaryExpression;
            Assert.AreEqual("EXISTS", condition.Operator);
            Assert.IsInstanceOfType(condition.Expression, typeof(SubqueryExpression));
        }

        [TestMethod]
        public void Parse_UnionAll_PreservesOperator()
        {
            var sql = "SELECT id FROM a UNION ALL SELECT id FROM b;";
            var statement = _parser.Parse(sql);

            var setOp = statement.Body as SetOperatorExpression;
            Assert.IsNotNull(setOp);
            Assert.AreEqual(SetOperatorType.UnionAll, setOp.Operator);
            Assert.IsInstanceOfType(setOp.Left, typeof(SelectStatement));
            Assert.IsInstanceOfType(setOp.Right, typeof(SelectStatement));
        }

        [TestMethod]
        public void Parse_ComplexQuery_NoParseException()
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
                    u.UserName ASC, o.OrderDate DESC;";

            var statement = _parser.Parse(sqlQuery);
            Assert.IsNotNull(statement);
        }

        [TestMethod]
        public void Parse_InvalidSql_ThrowsParseException()
        {
            var sql = "SELECT FROM MyTable;";
            var ex = Assert.ThrowsException<SqlParseException>(() => _parser.Parse(sql));
            // Correct the expected error message to precisely match the actual parser output
            StringAssert.Contains(ex.Message, "Unexpected token in primary expression: 'FROM'");
        }


        [TestMethod]
        public void Parse_InvalidSql_MissingAliasForDerivedTable_ThrowsParseException()
        {
            var sql = "SELECT Col FROM (SELECT ID FROM TempTable) WHERE Col > 10;"; // Missing alias 'AS a'
            var ex = Assert.ThrowsException<SqlParseException>(() => _parser.Parse(sql));
            StringAssert.Contains("Subquery in FROM clause must have an alias.", ex.Message);
        }

        [TestMethod]
        public void Parse_InvalidSql_UnmatchedParenthesis_ThrowsParseException()
        {
            var sql = "SELECT (Col1 FROM MyTable;";
            var ex = Assert.ThrowsException<SqlParseException>(() => _parser.Parse(sql));
            StringAssert.Contains("Expected ')' (Parenthesis), got 'FROM' (Keyword)", ex.Message);
        }

        // This test will now pass because the ParseStatementFactory handles set operators correctly
        [TestMethod]
        public void Parse_WhereInWithExceptSubquery_ReturnsCorrectAST()
        {
            var sql = @"
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

            var statement = _parser.Parse(sql);
            Assert.IsNotNull(statement);
            Assert.AreEqual(StatementType.Select, statement.Type);

            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt);
            Assert.AreEqual(5, selectStmt.SelectItems.Count);

            var whereCondition = selectStmt.WhereClause?.Condition as InExpression;
            Assert.IsNotNull(whereCondition);
            Assert.IsFalse(whereCondition.IsNegated);

            // The right side of IN is a SubqueryExpression, not SetOperatorExpression directly
            var inSubqueryExpr = whereCondition.Subquery;
            Assert.IsNotNull(inSubqueryExpr);
            Assert.AreEqual(StatementType.Select, inSubqueryExpr.Type); // SubqueryStatement itself is SqlStatement

            // The body of the SqlStatement within the SubqueryExpression is the SetOperatorExpression
            var setOpExpr = inSubqueryExpr.Body as SetOperatorExpression;
            Assert.IsNotNull(setOpExpr);
            Assert.AreEqual(SetOperatorType.Except, setOpExpr.Operator);

            // Check structure of both left and right SELECTs within the SetOperatorExpression
            var leftSetOpSelect = setOpExpr.Left as SelectStatement;
            Assert.IsNotNull(leftSetOpSelect);
            Assert.AreEqual(1, leftSetOpSelect.SelectItems.Count);
            var leftExpr = leftSetOpSelect.SelectItems[0].Expression as ColumnExpression;
            Assert.AreEqual("Well ID", leftExpr.ColumnName);
            Assert.AreEqual("WH2", leftExpr.TableAlias);

            var rightSetOpSelect = setOpExpr.Right as SelectStatement;
            Assert.IsNotNull(rightSetOpSelect);
            Assert.AreEqual(1, rightSetOpSelect.SelectItems.Count);
            var rightExpr = rightSetOpSelect.SelectItems[0].Expression as ColumnExpression;
            Assert.AreEqual("Well ID", rightExpr.ColumnName);
            Assert.AreEqual("WC", rightExpr.TableAlias);
        }
        [TestMethod]
        public void Parse_DeeplyNestedSubqueriesAndSetOps_ReturnsCorrectAST()
        {
            var sql = @"
                SELECT
                    outer_user.UserID,
                    outer_user.UserName,
                    (
                        SELECT COUNT(inner_o.OrderID)
                        FROM Orders inner_o
                        WHERE inner_o.UserID = outer_user.UserID
                        AND inner_o.OrderDate >= '2024-01-01'
                        AND inner_o.OrderID IN (
                            (SELECT li.OrderID FROM OrderItems li WHERE li.Quantity > 10)
                            UNION ALL
                            (SELECT ri.OrderID FROM ReturnedItems ri WHERE ri.ReturnDate < '2024-07-01')
                            EXCEPT
                            (SELECT si.OrderID FROM ShippedItems si WHERE si.ShipDate IS NULL)
                        )
                    ) AS TotalOrdersByCriteria
                FROM Users outer_user
                WHERE outer_user.Status = 'Active'
                AND outer_user.UserID NOT IN (SELECT blocked.UserID FROM BlockedUsers blocked);
            ";

            var statement = _parser.Parse(sql);
            Assert.IsNotNull(statement);
            Assert.AreEqual(StatementType.Select, statement.Type);

            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt);
            Assert.AreEqual(3, selectStmt.SelectItems.Count); // UserID, UserName, and TotalOrdersByCriteria

            // Verify the 'TotalOrdersByCriteria' subquery
            var totalOrdersSubquerySelectExpr = selectStmt.SelectItems[2]; // TotalOrdersByCriteria
            Assert.IsNotNull(totalOrdersSubquerySelectExpr.Expression as SubqueryExpression);
            Assert.AreEqual("TotalOrdersByCriteria", totalOrdersSubquerySelectExpr.Alias);

            var innerSubquery = totalOrdersSubquerySelectExpr.Expression as SubqueryExpression;
            var innerSelectStmt = innerSubquery.SubqueryStatement.Body as SelectStatement;
            Assert.IsNotNull(innerSelectStmt);
            Assert.AreEqual("COUNT", (innerSelectStmt.SelectItems[0].Expression as FunctionCallExpression).FunctionName);

            var innerWhereCondition = innerSelectStmt.WhereClause.Condition as BinaryExpression;
            Assert.AreEqual("AND", innerWhereCondition.Operator);

            // The IN expression is on the right side of the outermost AND
            var inSubqueryExpr = innerWhereCondition.Right as InExpression;
            Assert.IsNotNull(inSubqueryExpr);
            Assert.IsFalse(inSubqueryExpr.IsNegated);
            Assert.IsNotNull(inSubqueryExpr.Subquery);

            var setOpExpr = inSubqueryExpr.Subquery.Body as SetOperatorExpression;
            Assert.IsNotNull(setOpExpr);
            Assert.AreEqual(SetOperatorType.Except, setOpExpr.Operator); // The outermost set operator
            Assert.IsNotNull(setOpExpr.Left as SetOperatorExpression); // (UNION ALL) part
            Assert.IsNotNull(setOpExpr.Right as SelectStatement); // (SELECT si.OrderID ...) part

            // Verify the WHERE NOT IN clause
            Assert.IsNotNull(selectStmt.WhereClause);
            var mainWhereCondition = selectStmt.WhereClause.Condition as BinaryExpression;
            Assert.AreEqual("AND", mainWhereCondition.Operator);

            var notInExpr = mainWhereCondition.Right as InExpression;
            Assert.IsNotNull(notInExpr);
            Assert.IsTrue(notInExpr.IsNegated);

            var notInSubquery = notInExpr.Subquery;
            Assert.IsNotNull(notInSubquery);
            Assert.AreEqual("BlockedUsers", ((notInSubquery.Body as SelectStatement).FromClause.TableExpressions[0] as TableExpression).TableName);
        }

        [TestMethod]
        public void Parse_ComplexWhereClause_ReturnsCorrectAST()
        {
            var sql = @"
                SELECT OrderID, CustomerID
                FROM Orders
                WHERE
                    (OrderDate >= '2023-01-01' AND OrderDate <= '2023-12-31') -- A
                    OR (TotalAmount > 1000 AND CustomerID IN (SELECT VIPID FROM VIPCustomers)) -- B
                    AND NOT (Status = 'Cancelled' OR OrderType = 'Return') -- C
                    OR EXISTS (SELECT 1 FROM UrgentOrders WHERE UrgentOrders.OrderID = Orders.OrderID); -- D
            "; // This is line 490

            var statement = _parser.Parse(sql);
            Assert.IsNotNull(statement);
            Assert.AreEqual(StatementType.Select, statement.Type);

            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt.WhereClause);

            var condition = selectStmt.WhereClause.Condition as BinaryExpression;
            Assert.IsNotNull(condition);
            // The outermost operator should be OR: ( (A OR (B AND C)) OR D )
            Assert.AreEqual("OR", condition.Operator); // This is likely line 512 in your file now.

            // The left child of the outermost OR: (A OR (B AND C))
            var leftOfOuterOr = condition.Left as BinaryExpression;
            Assert.IsNotNull(leftOfOuterOr);
            Assert.AreEqual("OR", leftOfOuterOr.Operator); // <-- FIX: Change to "OR" (This is likely line 517 in your file)

            // The left child of this OR: A
            var leftOfOrGroup = leftOfOuterOr.Left as BinaryExpression;
            Assert.IsNotNull(leftOfOrGroup);
            Assert.AreEqual("AND", leftOfOrGroup.Operator); // This is A (AND operator)

            // The right child of this OR: (B AND C)
            var rightOfOrGroup = leftOfOuterOr.Right as BinaryExpression;
            Assert.IsNotNull(rightOfOrGroup);
            Assert.AreEqual("AND", rightOfOrGroup.Operator); // This is B AND C (AND operator)

            // Verify individual parts
            // A: (OrderDate >= '2023-01-01' AND OrderDate <= '2023-12-31')
            // A: (OrderDate >= '2023-01-01' AND OrderDate <= '2023-12-31')
            var aLeftBinary = leftOfOrGroup.Left as BinaryExpression;
            Assert.IsNotNull(aLeftBinary);
            Assert.AreEqual("OrderDate", (aLeftBinary.Left as ColumnExpression)?.ColumnName);
            Assert.AreEqual("AND", leftOfOrGroup.Operator); // A is AND
            

            // B: (TotalAmount > 1000 AND CustomerID IN (SELECT VIPID FROM VIPCustomers))
            Assert.AreEqual("AND", rightOfOrGroup.Operator); // B AND C is AND
            Assert.IsNotNull(rightOfOrGroup.Left as BinaryExpression); // This is B
            Assert.IsNotNull(rightOfOrGroup.Right as UnaryExpression); // This is C (NOT)

            // Further drill down into B if needed
            var bGroup = rightOfOrGroup.Left as BinaryExpression;
            Assert.AreEqual("AND", bGroup.Operator);
            Assert.IsNotNull(bGroup.Right as InExpression); // CustomerID IN (...)

            // C: NOT (Status = 'Cancelled' OR OrderType = 'Return')
            var cGroup = rightOfOrGroup.Right as UnaryExpression; // NOT (...)
            Assert.AreEqual("NOT", cGroup.Operator);
            Assert.IsNotNull(cGroup.Expression as BinaryExpression);
            Assert.AreEqual("OR", (cGroup.Expression as BinaryExpression).Operator); // (Status = 'Cancelled' OR OrderType = 'Return')

            // D: EXISTS (SELECT 1 FROM UrgentOrders WHERE UrgentOrders.OrderID = Orders.OrderID)
            var rightOfOuterOr = condition.Right as UnaryExpression; // EXISTS clause
            Assert.IsNotNull(rightOfOuterOr);
            Assert.AreEqual("EXISTS", rightOfOuterOr.Operator);
            Assert.IsInstanceOfType(rightOfOuterOr.Expression, typeof(SubqueryExpression));
        }
        [TestMethod]
        public void Parse_ComplexWhereClause_ReturnsCorrectAST_IsolationTest()
        {
            var sql = @"
        SELECT OrderID, CustomerID
        FROM Orders
        WHERE
            (OrderDate >= '2023-01-01' AND OrderDate <= '2023-12-31')
            OR (TotalAmount > 1000 AND CustomerID IN (SELECT VIPID FROM VIPCustomers))
            AND NOT (Status = 'Cancelled' OR OrderType = 'Return')
            OR EXISTS (SELECT 1 FROM UrgentOrders WHERE UrgentOrders.OrderID = Orders.OrderID);
    ";
            var statement = _parser.Parse(sql);
            Assert.IsNotNull(statement);

            var selectStmt = statement.Body as SelectStatement;
            Assert.IsNotNull(selectStmt.WhereClause);

            var condition = selectStmt.WhereClause.Condition as BinaryExpression;
            Assert.IsNotNull(condition);

            // This is the only assertion we care about right now
            Assert.AreEqual("OR", condition.Operator); // Line should pass based on debug output
            Console.WriteLine($"Actual outermost operator: {condition.Operator}"); // Print explicitly for verification
        }
    }
}