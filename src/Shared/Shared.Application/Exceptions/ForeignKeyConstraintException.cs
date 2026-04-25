// src/Shared/Application/Exceptions/ForeignKeyConstraintException.cs

using System;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.Exceptions
{
    public class ForeignKeyConstraintException : Exception
    {
        public List<string> BlockingTables { get; set; }

        public ForeignKeyConstraintException(string message, List<string> blockingTables) 
            : base(message)
        {
            BlockingTables = blockingTables;
        }
    }
}