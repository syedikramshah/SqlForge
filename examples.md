# SqlForge Examples

This document demonstrates the full API surface, plus a large set of SQL examples for both supported dialects.

## Package Overview

Primary APIs:

```csharp
using SqlForge;
using SqlForge.Enums;
using SqlForge.Formatters;
using SqlForge.Parsers;
using SqlForge.Reconstructors;
using SqlForge.Utils;
using SqlForge.Nodes;
```

## Factory-Based Usage

```csharp
var parserAny = SqlForgeFactory.CreateParser(SqlDialect.SqlAnywhere);
var parserMs = SqlForgeFactory.CreateParser(SqlDialect.MsSqlServer);

var reconAny = SqlForgeFactory.CreateReconstructor(SqlDialect.SqlAnywhere);
var reconMs = SqlForgeFactory.CreateReconstructor(SqlDialect.MsSqlServer);

var fmtAny = SqlForgeFactory.CreateFormatter(SqlDialect.SqlAnywhere);
var fmtMs = SqlForgeFactory.CreateFormatter(SqlDialect.MsSqlServer);

var ast1 = parserAny.Parse("SELECT id FROM users");
var ast2 = parserMs.Parse("SELECT TOP 5 id FROM users ORDER BY id DESC");

string sql1 = reconAny.Reconstruct(ast1);
string sql2 = reconMs.Reconstruct(ast2);

string fmt1 = fmtAny.Format(ast1);
string fmt2 = fmtMs.Format(ast2);
```

## Tokenizer API

```csharp
var tokenizer = new Tokenizer("SELECT [User ID] FROM [dbo].[Users]");
List<Token> tokens = tokenizer.Tokenize();

foreach (var token in tokens)
{
    Console.WriteLine($"{token.Type}: {token.Value}, Quote={token.QuoteStyle}");
}
```

Unicode string literal tokens in MSSQL:

```csharp
var tokenizer = new Tokenizer("SELECT N'Hello' AS greeting");
var tokens = tokenizer.Tokenize();

var literal = tokens.First(t => t.Type == TokenType.StringLiteral);
Console.WriteLine(literal.Value);       // Hello
Console.WriteLine(literal.IsUnicode);   // True
```

## Parser API

SQL Anywhere:

```csharp
var parser = SqlForgeFactory.CreateParser(SqlDialect.SqlAnywhere);

SqlStatement ast = parser.Parse("SELECT id, name FROM users WHERE active = 1");
```

Microsoft SQL Server:

```csharp
var parser = SqlForgeFactory.CreateParser(SqlDialect.MsSqlServer);

SqlStatement ast = parser.Parse("SELECT TOP 10 WITH TIES id FROM users ORDER BY id DESC");
```

## AST Inspection

```csharp
var ast = parser.Parse("SELECT id, name FROM users WHERE active = 1");

if (ast.Body is SelectStatement select)
{
    Console.WriteLine($"SelectItems: {select.SelectItems.Count}");
    Console.WriteLine($"HasFrom: {select.FromClause != null}");
    Console.WriteLine($"HasWhere: {select.WhereClause != null}");
    Console.WriteLine($"HasOrderBy: {select.OrderByClause != null}");
}
```

## Reconstructor API

```csharp
var reconstructor = new SqlAnywhereReconstructor();
var sql = reconstructor.Reconstruct(ast);
```

Dialect-specific reconstruction:

```csharp
var reconstructor = new MsSqlServerReconstructor();
var sql = reconstructor.Reconstruct(ast, SqlDialect.MsSqlServer);
```

## Formatter API

```csharp
var formatter = new SqlAnywhereFormatter();
var output = formatter.Format(ast);
```

## Manual AST Construction

```csharp
var select = new SelectStatement
{
    SelectItems = new List<SelectExpression>
    {
        new SelectExpression
        {
            Expression = new ColumnExpression("id")
        },
        new SelectExpression
        {
            Expression = new ColumnExpression("name")
        }
    },
    FromClause = new FromClause
    {
        TableExpressions = new List<TableExpression>
        {
            new TableExpression("users")
        }
    },
    WhereClause = new WhereClause
    {
        Condition = new BinaryExpression(
            new ColumnExpression("active"),
            "=",
            new LiteralExpression("1")
        )
    }
};

var statement = new SqlStatement
{
    Type = StatementType.Select,
    Body = select
};

var reconstructor = new SqlAnywhereReconstructor();
string sql = reconstructor.Reconstruct(statement);
```

## Error Handling

```csharp
try
{
    var ast = parser.Parse("SELECT FROM");
}
catch (SqlParseException ex)
{
    Console.WriteLine($"Error at position {ex.Position}: {ex.Message}");
}
```

## SQL Anywhere Query Examples

```sql
SELECT id FROM users;
SELECT id, name FROM users;
SELECT DISTINCT dept FROM employees;
SELECT price * quantity AS total FROM order_items;
SELECT id FROM users WHERE active = 1;
SELECT id FROM users WHERE id IN (1, 2, 3);
SELECT id FROM users WHERE id NOT IN (1, 2, 3);
SELECT id FROM users WHERE name LIKE 'A%';
SELECT id FROM users WHERE deleted_at IS NULL;
SELECT id FROM users WHERE deleted_at IS NOT NULL;
SELECT id FROM users WHERE (active = 1 OR role = 'admin') AND deleted_at IS NULL;
SELECT u.id, u.name FROM users u;
SELECT u.id, o.id FROM users u INNER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u LEFT OUTER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u RIGHT OUTER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u FULL OUTER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u CROSS JOIN orders o;
SELECT id FROM users WHERE EXISTS (SELECT 1 FROM orders WHERE orders.user_id = users.id);
SELECT id FROM users WHERE id IN (SELECT user_id FROM orders);
SELECT (SELECT MAX(amount) FROM payments) AS max_amount FROM users;
SELECT x.id FROM (SELECT id FROM users) AS x;
SELECT dept, COUNT(*) FROM employees GROUP BY dept;
SELECT dept, COUNT(*) FROM employees GROUP BY dept HAVING COUNT(*) > 5;
SELECT id, name FROM users ORDER BY name ASC, id DESC;
SELECT id FROM a UNION SELECT id FROM b;
SELECT id FROM a UNION ALL SELECT id FROM b;
SELECT id FROM a EXCEPT SELECT id FROM b;
SELECT id FROM a INTERSECT SELECT id FROM b;
(SELECT id FROM a UNION SELECT id FROM b) EXCEPT SELECT id FROM c;
SELECT COUNT(*) FROM users;
SELECT SUM(amount), AVG(amount), MIN(amount), MAX(amount) FROM payments;
SELECT SUBSTRING(name, 1, 10) FROM users;
SELECT GETDATE();
```

## Microsoft SQL Server Query Examples

```sql
SELECT id FROM users;
SELECT id, name FROM users;
SELECT DISTINCT dept FROM employees;
SELECT TOP 10 id FROM users;
SELECT TOP 5 PERCENT id FROM users;
SELECT TOP 10 WITH TIES score FROM leaderboard ORDER BY score DESC;
SELECT id FROM users ORDER BY id OFFSET 10 ROWS;
SELECT id FROM users ORDER BY id OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY;
SELECT id FROM users ORDER BY id OFFSET 0 ROWS FETCH FIRST 5 ROWS ONLY;
SELECT [User ID] FROM [dbo].[Users];
SELECT "User ID" FROM "dbo"."Users";
SELECT id FROM users WITH (NOLOCK);
SELECT id FROM users WITH (NOLOCK, READPAST);
SELECT u.id, o.id FROM users u INNER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u LEFT OUTER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u RIGHT OUTER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u FULL OUTER JOIN orders o ON u.id = o.user_id;
SELECT u.id, o.id FROM users u CROSS JOIN orders o;
SELECT u.id, x.last_order_id FROM users u CROSS APPLY (
    SELECT TOP 1 o.id AS last_order_id
    FROM orders o
    WHERE o.user_id = u.id
    ORDER BY o.id DESC
) x;
SELECT u.id, x.last_order_id FROM users u OUTER APPLY (
    SELECT TOP 1 o.id AS last_order_id
    FROM orders o
    WHERE o.user_id = u.id
    ORDER BY o.id DESC
) x;
SELECT COUNT(*) FROM users;
SELECT SUM(amount) OVER (ORDER BY id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_total FROM payments;
SELECT ROW_NUMBER() OVER (PARTITION BY dept ORDER BY salary DESC) AS rn FROM employees;
SELECT LAG(price, 1) OVER (ORDER BY dt) AS prev_price FROM stock_prices;
SELECT RANK() OVER (PARTITION BY dept ORDER BY salary DESC) AS rk FROM employees;
SELECT SUM(amount) OVER (ORDER BY id GROUPS BETWEEN 1 PRECEDING AND CURRENT ROW) AS grouped_sum FROM payments;
SELECT SUM(amount) OVER (PARTITION BY dept ORDER BY id RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) FROM payments;
WITH cte AS (SELECT id FROM users) SELECT id FROM cte;
WITH a AS (SELECT id FROM users), b AS (SELECT id FROM a) SELECT id FROM b;
WITH cte (user_id, user_name) AS (SELECT id, name FROM users) SELECT user_id FROM cte;
SELECT id FROM a UNION SELECT id FROM b;
SELECT id FROM a UNION ALL SELECT id FROM b;
SELECT id FROM a EXCEPT SELECT id FROM b;
SELECT id FROM a INTERSECT SELECT id FROM b;
(SELECT id FROM a UNION SELECT id FROM b) EXCEPT SELECT id FROM c;
SELECT id FROM users WHERE id IN (SELECT user_id FROM orders);
SELECT id FROM users WHERE EXISTS (SELECT 1 FROM orders WHERE orders.user_id = users.id);
SELECT id FROM users WHERE id NOT IN (1, 2, 3);
SELECT price * quantity AS total FROM order_items;
SELECT (SELECT MAX(amount) FROM payments) AS max_amount FROM users;
SELECT x.id FROM (SELECT id FROM users) AS x;
SELECT "First Name" AS "FN" FROM "dbo"."People";
SELECT [Column]]Name] FROM [Table]]Name];
SELECT id FROM users WHERE deleted_at IS NULL;
SELECT id FROM users WHERE deleted_at IS NOT NULL;
SELECT name FROM users WHERE name LIKE 'A%';
SELECT id FROM users WHERE (active = 1 OR role = 'admin') AND deleted_at IS NULL;
SELECT id, name FROM users ORDER BY name ASC, id DESC;
SELECT dept, COUNT(*) FROM employees GROUP BY dept HAVING COUNT(*) > 5;
WITH ranked AS (
    SELECT ROW_NUMBER() OVER (ORDER BY id) AS rn, id
    FROM users
)
SELECT id FROM ranked ORDER BY id OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY;
SELECT id FROM users WHERE bio = N'?????';
SELECT id FROM users WHERE name LIKE N'J%';
SELECT TOP 1 id FROM users WITH (INDEX(ix_users_id)) ORDER BY id DESC;
```

## Round-Trip Demonstration

```csharp
var ast = parser.Parse("SELECT id, name FROM users WHERE active = 1");
var sql = reconstructor.Reconstruct(ast);
var formatted = formatter.Format(ast);
```
