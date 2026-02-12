# SqlForge

[![NuGet](https://img.shields.io/nuget/v/SqlForge.svg)](https://www.nuget.org/packages/SqlForge/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0-blue.svg)](https://dotnet.microsoft.com/)

A powerful SQL parser, AST generator, and query reconstructor library for .NET. SqlForge tokenizes SQL queries, builds an Abstract Syntax Tree (AST), and can reconstruct or format SQL from the AST representation.

## Features

- **SQL Tokenization** - Full lexical analysis with support for keywords, identifiers, literals, operators, and comments
- **Dialect-Aware Keywords** - Token classification uses a dialect-aware keyword registry to reduce collisions
- **AST Generation** - Parse SQL into a traversable Abstract Syntax Tree
- **SQL Reconstruction** - Rebuild valid SQL from AST nodes
- **SQL Formatting** - Pretty-print AST for debugging and visualization
- **Dialect Support** - SQL Anywhere, Microsoft SQL Server, and MySQL parsers, reconstructors, and formatters
- **Complex Query Support** - Handles subqueries, JOINs, set operators, window functions, and nested expressions
- **MSSQL Extensions** - TOP, OFFSET/FETCH, CTEs, APPLY, table hints, and window frames

## Installation

### NuGet Package Manager

```powershell
Install-Package SqlForge
```

### .NET CLI

```bash
dotnet add package SqlForge
```

### PackageReference

```xml
<PackageReference Include="SqlForge" Version="1.2.0" />
```

## Quick Start

```csharp
using SqlForge.Parsers;
using SqlForge.Reconstructors;
using SqlForge.Formatters;
using SqlForge.Enums;
using SqlForge;

// Create parser via factory
var parser = SqlForgeFactory.CreateParser(SqlDialect.SqlAnywhere);

// Parse a SQL query
var ast = parser.Parse("SELECT id, name FROM users WHERE active = 1");

// Reconstruct the SQL
var reconstructor = new SqlAnywhereReconstructor();
string sql = reconstructor.Reconstruct(ast);
// Output: "SELECT id, name FROM users WHERE active = 1;"

// Format AST for debugging
var formatter = new SqlAnywhereFormatter();
string formatted = formatter.Format(ast);
```

```csharp
using SqlForge.Parsers;
using SqlForge.Reconstructors;
using SqlForge.Formatters;
using SqlForge.Enums;
using SqlForge;

// MSSQL parser and reconstructor
var msParser = SqlForgeFactory.CreateParser(SqlDialect.MsSqlServer);
var msAst = msParser.Parse("SELECT TOP 10 WITH TIES id FROM users ORDER BY id DESC");

var msReconstructor = new MsSqlServerReconstructor();
string msSql = msReconstructor.Reconstruct(msAst);

var msFormatter = new MsSqlServerFormatter();
string msFormatted = msFormatter.Format(msAst);
```

## API Reference

### Core Interfaces

| Interface | Description |
|-----------|-------------|
| `ISqlParser` | Parses SQL strings into AST |
| `ISqlReconstructor` | Reconstructs SQL from AST |
| `ISqlFormatter` | Formats AST for human-readable output |
| `ISqlNode` | Base interface for all AST nodes |

### Parsing

```csharp
// Create the parser
var parser = SqlForgeFactory.CreateParser(SqlDialect.SqlAnywhere);

// Parse SQL into AST
SqlStatement ast = parser.Parse("SELECT * FROM customers");

// Access the statement body
if (ast.Body is SelectStatement select)
{
    Console.WriteLine($"Columns: {select.SelectItems.Count}");
    Console.WriteLine($"Has WHERE: {select.WhereClause != null}");
}
```

```csharp
// Factory-based creation
var parserAny = SqlForgeFactory.CreateParser(SqlDialect.SqlAnywhere);
var parserMs = SqlForgeFactory.CreateParser(SqlDialect.MsSqlServer);

var reconAny = SqlForgeFactory.CreateReconstructor(SqlDialect.SqlAnywhere);
var reconMs = SqlForgeFactory.CreateReconstructor(SqlDialect.MsSqlServer);

var fmtAny = SqlForgeFactory.CreateFormatter(SqlDialect.SqlAnywhere);
var fmtMs = SqlForgeFactory.CreateFormatter(SqlDialect.MsSqlServer);

var ast1 = parserAny.Parse("SELECT id FROM users");
var ast2 = parserMs.Parse("SELECT TOP 5 id FROM users ORDER BY id DESC");
```

### Reconstruction

```csharp
var reconstructor = new SqlAnywhereReconstructor();

// Reconstruct with default dialect
string sql = reconstructor.Reconstruct(ast);

// Reconstruct with specific dialect
string sql = reconstructor.Reconstruct(ast, SqlDialect.SqlAnywhere);
```

Note: For Microsoft SQL Server, when an identifier was parsed as quoted but the original quote style is unknown, the reconstructor defaults to square brackets (`[name]`) to avoid reliance on `QUOTED_IDENTIFIER`.

Reconstructors are strict by design: if an AST node is not supported for the selected dialect, a `NotSupportedException` is thrown to avoid emitting invalid SQL. Treat this as a signal to extend the dialect implementation or pre-filter unsupported nodes.

### Tokenization

```csharp
using SqlForge.Utils;

var tokenizer = new Tokenizer("SELECT id FROM users");
List<Token> tokens = tokenizer.Tokenize();

foreach (var token in tokens)
{
    Console.WriteLine($"{token.Type}: {token.Value}");
}
// Output:
// Keyword: SELECT
// Identifier: id
// Keyword: FROM
// Identifier: users
// EOF:
```

```csharp
// Dialect-aware keyword tokenization
var tsqlTokenizer = new Tokenizer("SELECT MERGE FROM users", SqlDialect.MsSqlServer);
var sqlAnywhereTokenizer = new Tokenizer("SELECT MERGE FROM users", SqlDialect.SqlAnywhere);

Console.WriteLine(tsqlTokenizer.Tokenize()[1].Type);       // Keyword
Console.WriteLine(sqlAnywhereTokenizer.Tokenize()[1].Type); // Identifier
```

### AST Formatting

```csharp
var formatter = new SqlAnywhereFormatter();
string output = formatter.Format(ast);

// Output (hierarchical representation):
// SqlStatement:
//     Type: Select
//     SelectStatement:
//         SelectItems:
//             SelectExpression:
//                 Expression:
//                     ColumnExpression:
//                         Name: id
//         FromClause:
//             TableExpressions:
//                 TableExpression:
//                     TableName: users
```

## Supported SQL Features

Full examples for each dialect are in `examples.md`.

### SELECT Statements

```csharp
// Simple SELECT
parser.Parse("SELECT id, name FROM users");

// SELECT with aliases
parser.Parse("SELECT u.id AS user_id, u.name FROM users u");

// SELECT DISTINCT
parser.Parse("SELECT DISTINCT category FROM products");

// SELECT with expressions
parser.Parse("SELECT price * quantity AS total FROM orders");
```

### WHERE Clause

```csharp
// Simple conditions
parser.Parse("SELECT * FROM users WHERE active = 1");

// Complex conditions with AND/OR
parser.Parse("SELECT * FROM users WHERE active = 1 AND role = 'admin' OR id = 1");

// IN operator
parser.Parse("SELECT * FROM users WHERE id IN (1, 2, 3)");

// EXISTS subquery
parser.Parse("SELECT * FROM users u WHERE EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)");

// LIKE operator
parser.Parse("SELECT * FROM users WHERE name LIKE 'John%'");

// IS NULL / IS NOT NULL
parser.Parse("SELECT * FROM users WHERE deleted_at IS NULL");
```

### JOIN Operations

```csharp
// INNER JOIN
parser.Parse(@"
    SELECT u.name, o.order_date
    FROM users u
    INNER JOIN orders o ON u.id = o.user_id
");

// LEFT OUTER JOIN
parser.Parse(@"
    SELECT u.name, o.order_date
    FROM users u
    LEFT OUTER JOIN orders o ON u.id = o.user_id
");

// Multiple JOINs
parser.Parse(@"
    SELECT u.name, o.order_date, p.product_name
    FROM users u
    INNER JOIN orders o ON u.id = o.user_id
    INNER JOIN products p ON o.product_id = p.id
");

// APPLY (MSSQL)
parser.Parse(@"
    SELECT u.id, x.last_order_id
    FROM users u
    CROSS APPLY (
        SELECT TOP 1 o.id AS last_order_id
        FROM orders o
        WHERE o.user_id = u.id
        ORDER BY o.id DESC
    ) x
");
```

### Subqueries

```csharp
// Subquery in SELECT
parser.Parse(@"
    SELECT name, (SELECT COUNT(*) FROM orders o WHERE o.user_id = u.id) AS order_count
    FROM users u
");

// Subquery in FROM (derived table)
parser.Parse(@"
    SELECT * FROM (SELECT id, name FROM users WHERE active = 1) AS active_users
");

// Subquery in WHERE
parser.Parse(@"
    SELECT * FROM products
    WHERE category_id IN (SELECT id FROM categories WHERE name = 'Electronics')
");
```

### Set Operators

```csharp
// UNION
parser.Parse("SELECT id FROM users UNION SELECT id FROM admins");

// UNION ALL
parser.Parse("SELECT id FROM users UNION ALL SELECT id FROM admins");

// EXCEPT
parser.Parse("SELECT id FROM users EXCEPT SELECT id FROM banned_users");

// INTERSECT
parser.Parse("SELECT id FROM users INTERSECT SELECT id FROM premium_users");
```

### GROUP BY, HAVING, ORDER BY

```csharp
// GROUP BY with aggregate functions
parser.Parse(@"
    SELECT category, COUNT(*) AS count, SUM(price) AS total
    FROM products
    GROUP BY category
");

// GROUP BY with HAVING
parser.Parse(@"
    SELECT category, COUNT(*) AS count
    FROM products
    GROUP BY category
    HAVING COUNT(*) > 10
");

// ORDER BY
parser.Parse("SELECT * FROM users ORDER BY name ASC, created_at DESC");

// OFFSET/FETCH (MSSQL)
parser.Parse("SELECT id FROM users ORDER BY id OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY");
```

### Functions

```csharp
// Aggregate functions
parser.Parse("SELECT COUNT(*), SUM(amount), AVG(price), MIN(date), MAX(date) FROM orders");

// Scalar functions
parser.Parse("SELECT SUBSTRING(name, 1, 10) FROM users");
parser.Parse("SELECT GETDATE()");

// Window functions (MSSQL)
parser.Parse("SELECT ROW_NUMBER() OVER (PARTITION BY dept ORDER BY salary DESC) AS rn FROM employees");
parser.Parse("SELECT SUM(amount) OVER (ORDER BY id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) FROM payments");
```

## AST Node Types

### Statement Nodes

| Node | Description |
|------|-------------|
| `SqlStatement` | Root node containing statement type and body |
| `SelectStatement` | SELECT statement with all clauses |

### Expression Nodes

| Node | Description |
|------|-------------|
| `ColumnExpression` | Column reference (e.g., `table.column`) |
| `LiteralExpression` | Literal values (string, number, null) |
| `BinaryExpression` | Binary operations (e.g., `a = b`, `a AND b`) |
| `UnaryExpression` | Unary operations (e.g., `NOT x`, `EXISTS (...)`) |
| `FunctionCallExpression` | Function calls (e.g., `COUNT(*)`) |
| `SubqueryExpression` | Subquery with optional alias |
| `SetOperatorExpression` | Set operations (UNION, EXCEPT, INTERSECT) |

### Clause Nodes

| Node | Description |
|------|-------------|
| `SelectExpression` | Column in SELECT with optional alias |
| `FromClause` | FROM clause with table expressions |
| `TableExpression` | Table reference with optional alias |
| `JoinExpression` | JOIN operation with condition |
| `WhereClause` | WHERE condition |
| `GroupByClause` | GROUP BY expressions |
| `HavingClause` | HAVING condition |
| `OrderByClause` | ORDER BY items |
| `OrderItem` | Single ORDER BY item with direction |

## Enums

### SqlDialect

```csharp
public enum SqlDialect
{
    Generic,
    SqlAnywhere,
    MsSqlServer,
    MySql,
    PostgreSql
}
```

### TokenType

```csharp
public enum TokenType
{
    Keyword,
    Identifier,
    StringLiteral,
    NumericLiteral,
    Operator,
    Parenthesis,
    Comma,
    Semicolon,
    EOF
}
```

### JoinType

```csharp
public enum JoinType
{
    Unknown,
    Inner,
    Left,
    Right,
    Full,
    Cross,
    Natural,
    CrossApply,
    OuterApply
}
```

### QuoteStyle

```csharp
public enum QuoteStyle
{
    None,
    DoubleQuote,
    SquareBracket,
    Backtick
}
```

## Error Handling

SqlForge throws `SqlParseException` for parsing errors:

```csharp
try
{
    var ast = parser.Parse("SELECT FROM");  // Invalid SQL
}
catch (SqlParseException ex)
{
    Console.WriteLine($"Parse error at position {ex.Position}: {ex.Message}");
}
```

## Thread Safety

Parser, reconstructor, and formatter instances are **not thread-safe**. Create new instances for concurrent operations or use synchronization.

## Requirements

- .NET Standard 2.0 or higher
- Compatible with:
  - .NET Framework 4.6.1+
  - .NET Core 2.0+
  - .NET 5/6/7/8+

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Author

**Syed Ikram Shah**
- Website: [ikra.ms](https://ikra.ms)
- GitHub: [@syedikramshah](https://github.com/syedikramshah)

## Links

- [GitHub Repository](https://github.com/syedikramshah/SqlForge)
- [NuGet Package](https://www.nuget.org/packages/SqlForge/)
- [Architecture Documentation](ARCHITECTURE.md)
- [Changelog](CHANGELOG.md)
- [Examples](examples.md)
