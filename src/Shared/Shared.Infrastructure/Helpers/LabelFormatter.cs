using System.Text.RegularExpressions;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class LabelFormatter
    {
        /// <summary>
        /// Converts camelCase, snake_case, kebab-case to "Human Readable" format.
        /// E.g., "camelCase" => "Camel Case", "snake_case" => "Snake Case"
        /// </summary>
        public static string ToReadable(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            // Replace _ and - with space
            var text = input.Replace("_", " ").Replace("-", " ");

            // Add space before capital letters (camelCase)
            text = Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");

            // Capitalize first letter of each word
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Select(w => char.ToUpper(w[0]) + w.Substring(1));

            return string.Join(" ", words);
        }
    }
}
