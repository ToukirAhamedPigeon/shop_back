// src/Modules/UserTable/Application/DTOs/UserTableCombinationDTO.cs
using System;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs
{
    public class UserTableCombinationDTO
    {
        public string TableId { get; set; } = string.Empty;
        public List<string> ShowColumnCombinations { get; set; } = new List<string>();
    }
}
