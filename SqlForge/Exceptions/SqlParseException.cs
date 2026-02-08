using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlForge.Exceptions
{
    /// <summary>
    /// Custom exception for errors encountered during SQL parsing.
    /// Includes information about the position of the error.
    /// </summary>
    public class SqlParseException : Exception
    {
        public int Position { get; }
        public SqlParseException(string message, int position = -1) : base(message)
        {
            Position = position;
        }
    }

}
