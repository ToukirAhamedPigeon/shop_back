using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class NameExpander
    {
        // Define the expansion patterns
        private static readonly Dictionary<string, string[]> ExpansionPatterns = new()
        {
            { "CRUD", new[] { "Create", "Read", "Update", "Delete" } },
            { "RU", new[] { "Read", "Update" } },
            { "CRUDR", new[] { "Create", "Read", "Update", "Delete", "Restore" } },
            { "CRUDT", new[] { "Create", "Read", "Update", "Delete", "Trash" } },
            { "CRUDTR", new[] { "Create", "Read", "Update", "Delete", "Trash", "Restore" } },
            { "C", new[] { "Create" } },
            { "R", new[] { "Read" } },
            { "U", new[] { "Update" } },
            { "D", new[] { "Delete" } },
            { "T", new[] { "Trash" } },
            { "RESTORE", new[] { "Restore" } },
        };

        /// <summary>
        /// Expands shorthand notation like "CRUDR-admin-roles" to full permission names
        /// Example: "CRUDR-admin-roles" -> ["create-admin-roles", "read-admin-roles", "update-admin-roles", "delete-admin-roles", "restore-admin-roles"]
        /// </summary>
        public static List<string> ExpandNames(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<string>();

            var result = new List<string>();

            // Split by '=' first to handle multiple names
            var parts = input.Split('=', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            foreach (var part in parts)
            {
                // Check if the part contains a shorthand pattern
                var expanded = ExpandSingleName(part);
                result.AddRange(expanded);
            }

            return result.Distinct().ToList();
        }

        private static List<string> ExpandSingleName(string name)
        {
            var result = new List<string>();

            // Look for shorthand pattern at the beginning of the name
            foreach (var pattern in ExpansionPatterns)
            {
                if (name.StartsWith(pattern.Key + "-", StringComparison.OrdinalIgnoreCase))
                {
                    var suffix = name.Substring(pattern.Key.Length + 1); // +1 for the dash
                    foreach (var operation in pattern.Value)
                    {
                        // Convert operation to lowercase for consistency
                        var expandedName = $"{operation.ToLower()}-{suffix}";
                        result.Add(expandedName);
                    }
                    return result;
                }
            }

            // No pattern found, return the original name
            result.Add(name.ToLower());
            return result;
        }

        /// <summary>
        /// Expands a list of names (used for permissions in role creation)
        /// </summary>
        public static List<string> ExpandPermissionNames(List<string> permissionNames)
        {
            var expanded = new List<string>();
            foreach (var name in permissionNames)
            {
                expanded.AddRange(ExpandNames(name));
            }
            return expanded.Distinct().ToList();
        }
    }
}