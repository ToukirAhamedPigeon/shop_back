using System.Text.RegularExpressions;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class LabelFormatter
    {
        public static string ToReadable(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            // Normalize: replace hyphens and underscores with space
            var text = input.Replace("_", " ").Replace("-", " ");

            // Add space before camelCase capitals
            text = Regex.Replace(text, "([a-z0-9])([A-Z])", "$1 $2");

            // Split words and capitalize first letter of each
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Select(w =>
                            {
                                // Preserve acronyms fully uppercase (like ID, API)
                                if (w.All(char.IsUpper)) return w;
                                return char.ToUpper(w[0]) + w.Substring(1).ToLower();
                            });

            return string.Join(" ", words);
        }
    }
}
