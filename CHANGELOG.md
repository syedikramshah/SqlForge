# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-02-09

### Added
- Microsoft SQL Server dialect support (parser, reconstructor, formatter)
- TOP, OFFSET/FETCH, CTEs, APPLY, and table hint parsing
- Window function parsing with frame types and bounds
- Unicode string literal support (N'...')
- Quote style tracking for bracketed and double-quoted identifiers
- Factory helpers for creating dialect-specific parsers and utilities
- Expanded unit test coverage for MSSQL behavior

### Fixed
- Reconstruction precedence for boolean expressions with explicit parentheses
- NOT IN reconstruction formatting
- APPLY subquery reconstruction shape

## [1.0.0] - 2025-02-08

### Added
- Initial release
- SQL tokenizer with full token type support
- SQL Anywhere dialect parser
- Abstract Syntax Tree (AST) representation
- SQL reconstructor for AST-to-SQL conversion
- SQL formatter for pretty-printing
- Support for SELECT statements with all clauses
- JOIN operations support
- Subquery support
- Set operators (UNION, INTERSECT, EXCEPT)
