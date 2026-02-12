# SqlForge Architecture

This document provides a comprehensive overview of SqlForge's internal architecture, design patterns, and component interactions.

## Table of Contents

- [Overview](#overview)
- [High-Level Architecture](#high-level-architecture)
- [Processing Pipeline](#processing-pipeline)
- [Component Details](#component-details)
- [Design Patterns](#design-patterns)
- [Extensibility](#extensibility)
- [Directory Structure](#directory-structure)

## Overview

SqlForge is a SQL parsing library that transforms SQL query strings into Abstract Syntax Trees (AST) and back. The architecture follows a clean separation of concerns with distinct phases for tokenization, parsing, and reconstruction.

```
┌─────────────────────────────────────────────────────────────────────┐
│                         SqlForge Pipeline                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│   SQL String ──► Tokenizer ──► Parser ──► AST ──► Reconstructor     │
│                                            │                         │
│                                            └──► Formatter            │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

## High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                              Client Code                                  │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
            ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
            │  ISqlParser │ │ISqlReconstr.│ │ISqlFormatter│
            └──────┬──────┘ └──────┬──────┘ └──────┬──────┘
                   │               │               │
            ┌──────▼──────┐ ┌──────▼──────┐ ┌──────▼──────┐
            │SqlAnywhere  │ │SqlAnywhere  │ │SqlAnywhere  │
            │Parser       │ │Reconstructor│ │Formatter    │
            └──────┬──────┘ └─────────────┘ └─────────────┘
                   │
    ┌──────────────┼──────────────┐
    ▼              ▼              ▼
┌────────┐ ┌─────────────┐ ┌─────────────────────┐
│Tokenizer│ │ParserContext│ │StatementParserFactory│
└────────┘ └─────────────┘ └─────────────────────┘
                                    │
                          ┌─────────┴─────────┐
                          ▼                   ▼
                   ┌─────────────┐    ┌──────────────┐
                   │SelectStatement│   │ExpressionParser│
                   │Parser         │   │               │
                   └─────────────┘    └──────────────┘
```

## Processing Pipeline

### Phase 1: Tokenization

The `Tokenizer` class performs lexical analysis, converting raw SQL text into a stream of tokens. Keyword classification is dialect-aware, so token meaning can vary safely by SQL dialect.

```
SQL: "SELECT id, name FROM users WHERE active = 1"

Tokens:
  [Keyword: SELECT]
  [Identifier: id]
  [Comma: ,]
  [Identifier: name]
  [Keyword: FROM]
  [Identifier: users]
  [Keyword: WHERE]
  [Identifier: active]
  [Operator: =]
  [NumericLiteral: 1]
  [EOF]
```

**Token Types:**
- `Keyword` - SQL reserved words (SELECT, FROM, WHERE, etc.)
- `Identifier` - Table names, column names, aliases
- `StringLiteral` - Single-quoted strings ('value')
- `NumericLiteral` - Numbers (123, 45.67)
- `Operator` - Comparison and arithmetic operators (=, <, >, +, -, etc.)
- `Parenthesis` - ( and )
- `Comma` - ,
- `Semicolon` - ;
- `EOF` - End of input

**Special Handling:**
- Quoted identifiers: `"Column Name"` → Identifier with `IsQuoted=true`
- Escaped quotes: `'It''s'` → StringLiteral with value `It's`
- Comments: `-- comment` and `/* comment */` are skipped
- Dialect keyword disambiguation (for example, `MERGE` is a keyword in MS SQL Server but an identifier in SQL Anywhere)

### Phase 2: Parsing

The parser converts tokens into an Abstract Syntax Tree using recursive descent parsing.

```
                     SqlStatement
                          │
                          ▼
                    SelectStatement
                          │
         ┌────────────────┼────────────────┐
         ▼                ▼                ▼
   SelectItems       FromClause      WhereClause
         │                │                │
    ┌────┴────┐          ▼           BinaryExpr
    ▼         ▼    TableExpression    (active = 1)
ColumnExpr ColumnExpr  (users)
  (id)      (name)
```

**Parser Components:**

| Component | Responsibility |
|-----------|----------------|
| `SqlAnywhereParser` | Entry point, orchestrates parsing |
| `ParserContext` | Token stream management, lookahead |
| `StatementParserFactory` | Creates appropriate statement parsers |
| `SelectStatementParser` | Parses SELECT statements |
| `ExpressionParser` | Parses expressions (conditions, values) |

### Phase 3: Reconstruction

The `SqlAnywhereReconstructor` traverses the AST and generates valid SQL.

```
AST Traversal Order (for SELECT):
1. SELECT keyword
2. DISTINCT (if present)
3. SelectItems (comma-separated)
4. FROM clause with table expressions
5. WHERE clause
6. GROUP BY clause
7. HAVING clause
8. ORDER BY clause
9. Semicolon
```

### Phase 4: Formatting (Optional)

The `SqlAnywhereFormatter` produces a human-readable hierarchical representation of the AST for debugging.

## Component Details

### Tokenizer (`SqlForge.Utils.Tokenizer`)

```csharp
public class Tokenizer
{
    private readonly string _sql;
    private int _position;

    public Tokenizer(string sql, SqlDialect dialect = SqlDialect.Generic);
    public List<Token> Tokenize();
}
```

**Responsibilities:**
- Character-by-character scanning
- Token classification
- Dialect-aware keyword resolution via `DialectKeywordRegistry`
- Handling of string escapes and quoted identifiers
- Comment removal
- Error reporting for malformed input

### Dialect Keyword Registry (`SqlForge.Utils.DialectKeywordRegistry`)

The registry centralizes SQL keywords and allows dialect-specific additions and exclusions. It is used in both tokenizer classification and parser keyword checks to reduce keyword collisions across dialects.

### Parser Context (`SqlForge.Parsers.ParserContext`)

```csharp
public class ParserContext : IParserContext
{
    public Token PeekToken();
    public Token ConsumeToken(string expected, TokenType type);
    public bool MatchToken(string value, TokenType type);
    public bool IsKeyword(string keyword);
}
```

**Responsibilities:**
- Token stream navigation
- Lookahead support
- Token matching and consumption
- Dialect-aware keyword checks in `IsKeyword`, `MatchToken`, and `ConsumeToken`
- Position tracking for error messages

### Statement Parser Factory (`SqlForge.Parsers.SqlAnywhereStatementParserFactory`)

```csharp
public class SqlAnywhereStatementParserFactory : IStatementParserFactory
{
    public SqlStatement ParseStatement(IParserContext context);
}
```

**Responsibilities:**
- Determines statement type from initial tokens
- Delegates to appropriate statement parser
- Creates expression parser instances

### AST Nodes (`SqlForge.Nodes.*`)

All AST nodes implement `ISqlNode` and inherit from `AbstractSqlNode`:

```csharp
public interface ISqlNode { }

public abstract class AbstractSqlNode : ISqlNode { }
```

**Node Hierarchy:**

```
ISqlNode
├── AbstractSqlNode
│   ├── SqlStatement (root container)
│   ├── SelectStatement
│   ├── SelectExpression
│   ├── FromClause
│   ├── TableExpression
│   ├── JoinExpression
│   ├── WhereClause
│   ├── GroupByClause
│   ├── HavingClause
│   ├── OrderByClause
│   ├── OrderItem
│   ├── ColumnExpression
│   ├── LiteralExpression
│   ├── BinaryExpression
│   ├── UnaryExpression
│   ├── FunctionCallExpression
│   ├── SubqueryExpression
│   └── SetOperatorExpression
```

### Reconstructor (`SqlForge.Reconstructors.SqlAnywhereReconstructor`)

```csharp
public class SqlAnywhereReconstructor : BaseSqlReconstructor
{
    public override string Reconstruct(SqlStatement statement, SqlDialect dialect);
}
```

**Responsibilities:**
- AST traversal via pattern matching
- SQL syntax generation
- Proper parenthesization
- Quote handling for identifiers

## Design Patterns

### 1. Factory Pattern

`IStatementParserFactory` creates statement parsers based on the SQL being parsed:

```csharp
public interface IStatementParserFactory
{
    SqlStatement ParseStatement(IParserContext context);
}
```

### 2. Strategy Pattern

Different implementations of core interfaces allow dialect-specific behavior:

- `ISqlParser` → `SqlAnywhereParser`
- `ISqlReconstructor` → `SqlAnywhereReconstructor`
- `ISqlFormatter` → `SqlAnywhereFormatter`

### 3. Visitor Pattern (Implicit)

The reconstructor and formatter use switch-based pattern matching to handle different node types, which is a simplified visitor pattern:

```csharp
switch (node)
{
    case SelectStatement select: // handle
    case ColumnExpression col:   // handle
    case BinaryExpression bin:   // handle
    // ...
}
```

### 4. Composite Pattern

AST nodes form a tree structure where composite nodes contain child nodes:

```csharp
public class SelectStatement : AbstractSqlNode
{
    public List<SelectExpression> SelectItems { get; set; }
    public FromClause FromClause { get; set; }
    public WhereClause WhereClause { get; set; }
    // ...
}
```

## Extensibility

### Adding a New SQL Dialect

1. **Create dialect-specific parser:**
   ```csharp
   public class MySqlParser : BaseSqlParser
   {
       public override SqlStatement Parse(string sql, SqlDialect dialect);
   }
   ```

2. **Create dialect-specific reconstructor:**
   ```csharp
   public class MySqlReconstructor : BaseSqlReconstructor
   {
       public override string Reconstruct(SqlStatement statement, SqlDialect dialect);
   }
   ```

3. **Create statement parser factory:**
   ```csharp
   public class MySqlStatementParserFactory : IStatementParserFactory
   {
       public SqlStatement ParseStatement(IParserContext context);
   }
   ```

### Adding New Statement Types

1. **Create new node class:**
   ```csharp
   public class InsertStatement : AbstractSqlNode
   {
       public string TableName { get; set; }
       public List<string> Columns { get; set; }
       public List<ISqlNode> Values { get; set; }
   }
   ```

2. **Add to StatementType enum:**
   ```csharp
   public enum StatementType
   {
       Select,
       Insert,  // New
       Update,
       Delete
   }
   ```

3. **Create statement parser:**
   ```csharp
   public class InsertStatementParser : IStatementParser
   {
       public InsertStatement Parse(IParserContext context);
   }
   ```

4. **Update factory to handle new statement:**
   ```csharp
   if (context.IsKeyword("INSERT"))
       return _insertParser.Parse(context);
   ```

5. **Update reconstructor to handle new node:**
   ```csharp
   case InsertStatement insert:
       _sb.Append("INSERT INTO ");
       // ...
   ```

### Adding New Expression Types

1. **Create expression node:**
   ```csharp
   public class CaseExpression : AbstractSqlNode
   {
       public List<WhenClause> WhenClauses { get; set; }
       public ISqlNode ElseExpression { get; set; }
   }
   ```

2. **Update expression parser to recognize and parse**

3. **Update reconstructor to generate SQL**

## Directory Structure

```
SqlForge/
├── SqlForge/                      # Main library
│   ├── Enums/                     # Enumerations
│   │   ├── JoinType.cs
│   │   ├── LiteralType.cs
│   │   ├── SetOperatorType.cs
│   │   ├── SqlDialect.cs
│   │   ├── StatementType.cs
│   │   └── TokenType.cs
│   │
│   ├── Exceptions/                # Custom exceptions
│   │   └── SqlParseException.cs
│   │
│   ├── Formatters/                # AST formatters
│   │   ├── BaseSqlFormatter.cs
│   │   └── SqlAnywhereFormatter.cs
│   │
│   ├── Interfaces/                # Core interfaces
│   │   ├── IExpressionParser.cs
│   │   ├── IParserContext.cs
│   │   ├── ISelectClauseParser.cs
│   │   ├── ISqlFormatter.cs
│   │   ├── ISqlNode.cs
│   │   ├── ISqlParser.cs
│   │   ├── ISqlReconstructor.cs
│   │   ├── IStatementParser.cs
│   │   └── IStatementParserFactory.cs
│   │
│   ├── Nodes/                     # AST node classes
│   │   ├── AbstractSqlNode.cs
│   │   ├── BinaryExpression.cs
│   │   ├── ColumnExpression.cs
│   │   ├── FromClause.cs
│   │   ├── FunctionCallExpression.cs
│   │   ├── GroupByClause.cs
│   │   ├── HavingClause.cs
│   │   ├── InExpression.cs
│   │   ├── JoinExpression.cs
│   │   ├── LiteralExpression.cs
│   │   ├── OrderByClause.cs
│   │   ├── OrderItem.cs
│   │   ├── SelectExpression.cs
│   │   ├── SelectStatement.cs
│   │   ├── SetOperatorExpression.cs
│   │   ├── SqlStatement.cs
│   │   ├── SubqueryExpression.cs
│   │   ├── TableExpression.cs
│   │   ├── UnaryExpression.cs
│   │   └── WhereClause.cs
│   │
│   ├── Parsers/                   # Parser implementations
│   │   ├── BaseSqlParser.cs
│   │   ├── ParserContext.cs
│   │   ├── SelectStatementParser.cs
│   │   ├── SqlAnywhereExpressionParser.cs
│   │   ├── SqlAnywhereParser.cs
│   │   ├── SqlAnywhereStatementParserFactory.cs
│   │   └── StatementParserFactoryHolder.cs
│   │
│   ├── Reconstructors/            # SQL reconstructors
│   │   ├── BaseSqlReconstructor.cs
│   │   └── SqlAnywhereReconstructor.cs
│   │
│   ├── Utils/                     # Utility classes
│   │   ├── Token.cs
│   │   └── Tokenizer.cs
│   │
│   └── SqlForge.csproj
│
├── SqlForge.Tests/                # Unit tests
│   ├── FormatterTests.cs
│   ├── ParserTests.cs
│   ├── ReconstructorTests.cs
│   ├── TokenizerTests.cs
│   └── SqlForge.Tests.csproj
│
├── .gitignore
├── ARCHITECTURE.md                # This file
├── CHANGELOG.md
├── LICENSE
├── README.md
└── SqlForge.sln
```

## Dialect Parity Notes

SqlForge targets practical parity across supported dialects while keeping a shared AST. The notes below summarize what is implemented for SQL Anywhere and MS SQL Server and highlight remaining gaps.

### SQL Anywhere

**Implemented:**
- SELECT pipeline (joins, grouping, ordering)
- DML statements: INSERT, UPDATE, DELETE
- DDL statements: CREATE TABLE, DROP TABLE, ALTER TABLE
- Column options: NULL/NOT NULL, DEFAULT, PRIMARY KEY, UNIQUE, IDENTITY
- Table constraints: PRIMARY KEY, UNIQUE

**Remaining gaps (not yet modeled):**
- CREATE INDEX / DROP INDEX
- FOREIGN KEY and CHECK constraints
- Computed columns and persisted attributes
- Table-level options and storage attributes
- Advanced locking clauses

### MS SQL Server

**Implemented:**
- SELECT pipeline (including TOP and OFFSET/FETCH)
- DML statements: INSERT, UPDATE, DELETE
- DDL statements: CREATE TABLE, DROP TABLE, ALTER TABLE
- Column options: NULL/NOT NULL, DEFAULT, PRIMARY KEY, UNIQUE, IDENTITY
- Table constraints: PRIMARY KEY, UNIQUE

**Remaining gaps (not yet modeled):**
- MERGE and OUTPUT clauses
- CREATE/ALTER/DROP INDEX
- FOREIGN KEY and CHECK constraints, computed columns
- Filegroup/table options, partitioning, advanced index options
- RENAME support (SQL Server typically uses sp_rename)

## Data Flow Example

Here's a complete trace of parsing `SELECT id FROM users`:

```
1. INPUT: "SELECT id FROM users"

2. TOKENIZATION:
   Tokenizer.Tokenize()
   → [Keyword:SELECT, Identifier:id, Keyword:FROM, Identifier:users, EOF]

3. PARSING:
   SqlAnywhereParser.Parse()
   → ParserContext(tokens)
   → StatementParserFactory.ParseStatement()
   → SelectStatementParser.Parse()
      → ParseSelectClause() → [SelectExpression(ColumnExpression(id))]
      → ParseFromClause() → FromClause([TableExpression(users)])
   → SqlStatement(type=Select, body=SelectStatement)

4. AST:
   SqlStatement
   └── SelectStatement
       ├── SelectItems: [SelectExpression]
       │   └── Expression: ColumnExpression(ColumnName="id")
       └── FromClause
           └── TableExpressions: [TableExpression]
               └── TableName="users"

5. RECONSTRUCTION:
   SqlAnywhereReconstructor.Reconstruct()
   → "SELECT id FROM users;"

6. FORMATTING:
   SqlAnywhereFormatter.Format()
   → "SqlStatement:\n    Type: Select\n    SelectStatement:\n..."
```

## Performance Considerations

- **Single-pass tokenization**: O(n) where n is SQL length
- **Recursive descent parsing**: O(n) for most queries, O(n log n) for deeply nested subqueries
- **AST traversal**: O(m) where m is number of nodes
- **Memory**: AST nodes are created on the heap; consider object pooling for high-throughput scenarios

## Future Considerations

Planned enhancements and known limitations are tracked in [LIMITATIONS_AND_FUTURE_WORK.md](LIMITATIONS_AND_FUTURE_WORK.md).
