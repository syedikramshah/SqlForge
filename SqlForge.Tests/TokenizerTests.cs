using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlForge.Enums;
using SqlForge.Exceptions;
using SqlForge.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlForge.Tests
{
    [TestClass]
    public class TokenizerTests
    {
        [TestMethod]
        public void Tokenize_EmptyString_ReturnsEOF()
        {
            var tokenizer = new Tokenizer("");
            var tokens = tokenizer.Tokenize();
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenType.EOF, tokens[0].Type);
        }

        [TestMethod]
        public void Tokenize_WhitespaceOnly_ReturnsEOF()
        {
            var tokenizer = new Tokenizer("   \t\n\r ");
            var tokens = tokenizer.Tokenize();
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenType.EOF, tokens[0].Type);
        }

        [TestMethod]
        public void Tokenize_Keywords_CorrectlyIdentified()
        {
            // Added OUTER to the test string and expected types
            var tokenizer = new Tokenizer("SELECT FROM WHERE AND OR GROUP BY HAVING ORDER AS INNER LEFT RIGHT FULL ON LIKE IN NOT NULL IS EXISTS OUTER LIMIT");
            var tokens = tokenizer.Tokenize();
            var expectedTypes = new List<TokenType>
            {
                TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword,
                TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword,
                TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword,
                TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.Keyword,
                TokenType.Keyword, TokenType.Keyword, TokenType.Keyword, TokenType.EOF // Added TokenType.Keyword for OUTER
            };
            CollectionAssert.AreEqual(expectedTypes, tokens.Select(t => t.Type).ToList());
            Assert.AreEqual("SELECT", tokens[0].Value);
            Assert.AreEqual("FROM", tokens[1].Value);
            Assert.AreEqual("OUTER", tokens[21].Value);
        }

        [TestMethod]
        public void Tokenize_Identifiers_CorrectlyIdentified()
        {
            var tokenizer = new Tokenizer("myTable Column_Name another_id");
            var tokens = tokenizer.Tokenize();
            Assert.AreEqual(TokenType.Identifier, tokens[0].Type);
            Assert.AreEqual("myTable", tokens[0].Value);
            Assert.AreEqual(TokenType.Identifier, tokens[1].Type);
            Assert.AreEqual("Column_Name", tokens[1].Value);
            Assert.AreEqual(TokenType.Identifier, tokens[2].Type);
            Assert.AreEqual("another_id", tokens[2].Value);
        }

        [TestMethod]
        public void Tokenize_StringLiterals_CorrectlyIdentified()
        {
            var tokenizer = new Tokenizer("SELECT * FROM my_table WHERE name = 'John'");
            var tokens = tokenizer.Tokenize();

            var stringToken = tokens.FirstOrDefault(t => t.Value == "John");
            Assert.IsNotNull(stringToken);
            Assert.AreEqual(TokenType.StringLiteral, stringToken.Type);
        }
        [TestMethod]
        public void Tokenize_NumericLiterals_CorrectlyIdentified()
        {
            var tokenizer = new Tokenizer("123 45.67 0.89 100");
            var tokens = tokenizer.Tokenize();
            Assert.AreEqual(TokenType.NumericLiteral, tokens[0].Type);
            Assert.AreEqual("123", tokens[0].Value);
            Assert.AreEqual(TokenType.NumericLiteral, tokens[1].Type);
            Assert.AreEqual("45.67", tokens[1].Value);
            Assert.AreEqual(TokenType.NumericLiteral, tokens[2].Type);
            Assert.AreEqual("0.89", tokens[2].Value);
            Assert.AreEqual(TokenType.NumericLiteral, tokens[3].Type);
            Assert.AreEqual("100", tokens[3].Value);
        }

        [TestMethod]
        public void Tokenize_Operators_CorrectlyIdentified()
        {
            var tokenizer = new Tokenizer("= <> != < > <= >= <=> + - * / % & | ^ && || << >> ~ .");
            var tokens = tokenizer.Tokenize();
            var expectedValues = new List<string> { "=", "<>", "!=", "<", ">", "<=", ">=", "<=>", "+", "-", "*", "/", "%", "&", "|", "^", "&&", "||", "<<", ">>", "~", "." };
            CollectionAssert.AreEqual(expectedValues, tokens.Take(expectedValues.Count).Select(t => t.Value).ToList());
            Assert.IsTrue(tokens.Take(expectedValues.Count).All(t => t.Type == TokenType.Operator));
            Assert.AreEqual(TokenType.EOF, tokens.Last().Type);
        }

        [TestMethod]
        public void Tokenize_Delimiters_CorrectlyIdentified()
        {
            var tokenizer = new Tokenizer("(),;");
            var tokens = tokenizer.Tokenize();
            Assert.AreEqual(TokenType.Parenthesis, tokens[0].Type);
            Assert.AreEqual("(", tokens[0].Value);
            Assert.AreEqual(TokenType.Parenthesis, tokens[1].Type);
            Assert.AreEqual(")", tokens[1].Value);
            Assert.AreEqual(TokenType.Comma, tokens[2].Type);
            Assert.AreEqual(",", tokens[2].Value);
            Assert.AreEqual(TokenType.Semicolon, tokens[3].Type);
            Assert.AreEqual(";", tokens[3].Value);
        }

        [TestMethod]
        public void Tokenize_SingleLineComments_Ignored()
        {
            var tokenizer = new Tokenizer("SELECT -- This is a comment\n1 FROM Dual;");
            var tokens = tokenizer.Tokenize();
            Assert.AreEqual(6, tokens.Count); // SELECT, 1, FROM, Dual, ;, EOF
            Assert.AreEqual("SELECT", tokens[0].Value);
            Assert.AreEqual("1", tokens[1].Value);
            Assert.AreEqual("FROM", tokens[2].Value);
            Assert.AreEqual("Dual", tokens[3].Value);
            Assert.AreEqual(";", tokens[4].Value);
            Assert.AreEqual(TokenType.EOF, tokens[5].Type);
        }

        [TestMethod]
        public void Tokenize_MultiLineComments_Ignored()
        {
            var tokenizer = new Tokenizer("SELECT /* This is a\nmulti-line comment */ 1 FROM Dual;");
            var tokens = tokenizer.Tokenize();
            Assert.AreEqual(6, tokens.Count); // SELECT, 1, FROM, Dual, ;, EOF
            Assert.AreEqual("SELECT", tokens[0].Value);
            Assert.AreEqual("1", tokens[1].Value);
            Assert.AreEqual("FROM", tokens[2].Value);
            Assert.AreEqual("Dual", tokens[3].Value);
            Assert.AreEqual(";", tokens[4].Value);
            Assert.AreEqual(TokenType.EOF, tokens[5].Type);
        }

        [TestMethod]
        public void Tokenize_MixedTokens_CorrectSequence()
        {
            var sql = "SELECT a.col1, b.col2 FROM TableA AS a INNER JOIN TableB b ON a.id = b.id WHERE a.status = 'Active' AND b.value > 100;";
            var tokenizer = new Tokenizer(sql);
            var tokens = tokenizer.Tokenize();

            var expectedValues = new List<string>
            {
                "SELECT", "a", ".", "col1", ",", "b", ".", "col2", "FROM", "TableA", "AS", "a",
                "INNER", "JOIN", "TableB", "b", "ON", "a", ".", "id", "=", "b", ".", "id",
                "WHERE", "a", ".", "status", "=", "Active", "AND", "b", ".", "value", ">", "100", ";"
            };
            var actualValues = tokens.Take(tokens.Count - 1).Select(t => t.Value).ToList(); // Exclude EOF
            CollectionAssert.AreEqual(expectedValues, actualValues);
        }

        [TestMethod]
        public void Tokenize_UnterminatedStringLiteral_ThrowsException()
        {
            var tokenizer = new Tokenizer("SELECT 'unterminated string");
            var ex = Assert.ThrowsException<SqlParseException>(() => tokenizer.Tokenize());
            StringAssert.Contains("Unterminated string literal starting at 7", ex.Message);
        }

        [TestMethod]
        public void Tokenize_UnterminatedQuotedIdentifier_ThrowsException()
        {
            var tokenizer = new Tokenizer("SELECT \"unterminated");
            var ex = Assert.ThrowsException<SqlParseException>(() => tokenizer.Tokenize());
            StringAssert.Contains("Unterminated quoted identifier starting at 7", ex.Message);
        }

        [TestMethod]
        public void Tokenize_UnterminatedMultiLineComment_ThrowsException()
        {
            var tokenizer = new Tokenizer("SELECT /* unterminated comment");
            var ex = Assert.ThrowsException<SqlParseException>(() => tokenizer.Tokenize());
            StringAssert.Contains("Unterminated multi-line comment", ex.Message);
            Assert.AreEqual(7, ex.Position);
        }

        [TestMethod]
        public void Tokenize_UnexpectedCharacter_ThrowsException()
        {
            var tokenizer = new Tokenizer("SELECT #invalid");
            var ex = Assert.ThrowsException<SqlParseException>(() => tokenizer.Tokenize());
            StringAssert.Contains("Unexpected character '#' at position 7", ex.Message);
        }
        [TestMethod]
        public void Tokenize_QuotedIdentifier_CorrectlyIdentified()
        {
            // Arrange
            var sql = "SELECT \"Well ID\" FROM WellHeader";
            var tokenizer = new Tokenizer(sql);

            // Act
            var tokens = tokenizer.Tokenize();

            // Assert
            Assert.IsTrue(tokens.Exists(t => t.Type == TokenType.Identifier && t.Value == "Well ID"),
                "Quoted identifier was not correctly recognized.");
        }

        [TestMethod]
        public void Tokenize_StringLiteral_CorrectlyIdentified()
        {
            // Arrange
            var sql = "WHERE Name = 'Oil Well'";
            var tokenizer = new Tokenizer(sql);

            // Act
            var tokens = tokenizer.Tokenize();

            // Assert
            Assert.IsTrue(tokens.Exists(t => t.Type == TokenType.StringLiteral && t.Value == "Oil Well"),
                "String literal was not correctly recognized.");
        }

        [TestMethod]
        public void Tokenize_QuotedIdentifier_DoesNotBecomeStringLiteral()
        {
            // Arrange
            var sql = "SELECT \"Name with Space\" FROM Wells";
            var tokenizer = new Tokenizer(sql);

            // Act
            var tokens = tokenizer.Tokenize();

            // Assert
            var quoted = tokens.Find(t => t.Value == "Name with Space");
            Assert.IsNotNull(quoted, "Quoted identifier not found.");
            Assert.AreEqual(TokenType.Identifier, quoted.Type, "Quoted identifier incorrectly classified as something else.");
        }

        [TestMethod]
        public void Tokenize_EscapedQuoteInStringLiteral_HandledCorrectly()
        {
            // Arrange
            var sql = "WHERE Note = 'It''s producing'";
            var tokenizer = new Tokenizer(sql);

            // Act
            var tokens = tokenizer.Tokenize();

            // Assert
            var literal = tokens.Find(t => t.Type == TokenType.StringLiteral);
            Assert.IsNotNull(literal, "String literal not found.");
            Assert.AreEqual("It's producing", literal.Value, "Escaped quote was not interpreted correctly.");
        }

        [TestMethod]
        public void Tokenize_DoubleQuoteInsideQuotedIdentifier_EscapedCorrectly()
        {
            // Arrange
            var sql = "SELECT \"Well\"\"ID\" FROM Wells";
            var tokenizer = new Tokenizer(sql);

            // Act
            var tokens = tokenizer.Tokenize();

            // Assert
            var identifier = tokens.Find(t => t.Type == TokenType.Identifier);
            Assert.IsNotNull(identifier, "Escaped quoted identifier not found.");
            Assert.AreEqual("Well\"ID", identifier.Value, "Escaped double-quote in identifier not handled properly.");
        }

        [TestMethod]
        public void Tokenize_BracketedIdentifier_EscapedCorrectly()
        {
            var sql = "SELECT [Well]]ID] FROM [Wells]]Archive]";
            var tokenizer = new Tokenizer(sql);

            var tokens = tokenizer.Tokenize();

            var firstIdentifier = tokens.First(t => t.Type == TokenType.Identifier);
            var secondIdentifier = tokens.Where(t => t.Type == TokenType.Identifier).Skip(1).First();

            Assert.AreEqual("Well]ID", firstIdentifier.Value);
            Assert.IsTrue(firstIdentifier.IsQuoted);
            Assert.IsTrue(firstIdentifier.IsSquareBracketed);
            Assert.AreEqual("Wells]Archive", secondIdentifier.Value);
            Assert.IsTrue(secondIdentifier.IsSquareBracketed);
        }

        [TestMethod]
        public void Tokenize_BacktickIdentifier_EscapedCorrectly()
        {
            var sql = "SELECT `Well``ID` FROM `Wells``Archive`";
            var tokenizer = new Tokenizer(sql);

            var tokens = tokenizer.Tokenize();

            var firstIdentifier = tokens.First(t => t.Type == TokenType.Identifier);
            var secondIdentifier = tokens.Where(t => t.Type == TokenType.Identifier).Skip(1).First();

            Assert.AreEqual("Well`ID", firstIdentifier.Value);
            Assert.IsTrue(firstIdentifier.IsQuoted);
            Assert.IsTrue(firstIdentifier.IsBacktickQuoted);
            Assert.AreEqual("Wells`Archive", secondIdentifier.Value);
            Assert.IsTrue(secondIdentifier.IsBacktickQuoted);
        }

        [TestMethod]
        public void Tokenize_BracketedIdentifier_Unterminated_ThrowsException()
        {
            var tokenizer = new Tokenizer("SELECT [WellName FROM Wells");
            var ex = Assert.ThrowsException<SqlParseException>(() => tokenizer.Tokenize());
            StringAssert.Contains(ex.Message, "Unterminated bracketed identifier");
        }

        [TestMethod]
        public void Tokenize_BacktickIdentifier_Unterminated_ThrowsException()
        {
            var tokenizer = new Tokenizer("SELECT `WellName FROM Wells");
            var ex = Assert.ThrowsException<SqlParseException>(() => tokenizer.Tokenize());
            StringAssert.Contains(ex.Message, "Unterminated backtick identifier");
        }

        [TestMethod]
        public void Tokenize_UnicodePrefixStringLiteral_RecognizedAsSingleStringToken()
        {
            var tokenizer = new Tokenizer("SELECT N'Abc', n'XyZ'");
            var tokens = tokenizer.Tokenize();

            var stringTokens = tokens.Where(t => t.Type == TokenType.StringLiteral).ToList();
            Assert.AreEqual(2, stringTokens.Count);
            Assert.AreEqual("Abc", stringTokens[0].Value);
            Assert.AreEqual("XyZ", stringTokens[1].Value);
        }

        [TestMethod]
        public void Tokenize_QuotedIdentifier_TracksQuoteStyle()
        {
            var tokenizer = new Tokenizer("SELECT \"A\", [B], `C` FROM T");
            var tokens = tokenizer.Tokenize();

            var quotedIdentifiers = tokens.Where(t => t.Type == TokenType.Identifier && t.IsQuoted).ToList();
            Assert.AreEqual(3, quotedIdentifiers.Count);
            Assert.IsTrue(quotedIdentifiers.Any(t => t.Value == "A" && t.IsDoubleQuoted));
            Assert.IsTrue(quotedIdentifiers.Any(t => t.Value == "B" && t.IsSquareBracketed));
            Assert.IsTrue(quotedIdentifiers.Any(t => t.Value == "C" && t.IsBacktickQuoted));
        }

        [TestMethod]
        public void Tokenize_DoesNotWriteDebugOutput()
        {
            var originalOut = Console.Out;
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                var tokenizer = new Tokenizer("SELECT 1");
                tokenizer.Tokenize();
                Assert.AreEqual(string.Empty, writer.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                writer.Dispose();
            }
        }
    }

}
