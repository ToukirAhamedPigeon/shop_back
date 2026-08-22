// src/Shared/Shared.Infrastructure/Helpers/DocPathResolver.cs
using System.Text.RegularExpressions;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    /// <summary>
    /// Resolves user-supplied documentation "slugs" to absolute .md file paths in a way that is
    /// guaranteed to stay inside a configured base directory.
    ///
    /// Guarantee: <see cref="TryResolveMarkdownPath"/> returns a non-null path only if the slug
    /// (a) matched the character allowlist in <see cref="IsValidSlug"/> (which excludes "..",
    /// backslashes, drive letters, and any URL-encoded traversal sequence, since none of those
    /// characters are permitted), AND (b) the fully-qualified resolved path is contained within
    /// the fully-qualified base directory. Any other outcome (invalid slug, missing file, or an
    /// attempt to escape the base directory) is surfaced identically to callers so that no
    /// information about the filesystem outside the base directory is ever leaked.
    /// </summary>
    public static class DocPathResolver
    {
        // Allowlist (not denylist): only letters, digits, underscore, hyphen and forward slash.
        // This rejects "..", absolute paths, drive letters (":"), backslashes, and any
        // URL-decoded traversal attempt, because none of those characters are permitted.
        private static readonly Regex AllowedSlugPattern = new(@"^[A-Za-z0-9_\-/]+$", RegexOptions.Compiled);

        /// <summary>
        /// True if <paramref name="slug"/> is non-empty and contains only characters in the
        /// allowlist [A-Za-z0-9_-/].
        /// </summary>
        public static bool IsValidSlug(string? slug) =>
            !string.IsNullOrEmpty(slug) && AllowedSlugPattern.IsMatch(slug);

        /// <summary>
        /// Resolves "{baseDir}/{subFolder}/{slug}.md" to a fully-qualified path.
        /// Returns null when the slug fails <see cref="IsValidSlug"/>, or when the resolved path
        /// would fall outside <paramref name="baseDir"/>. Callers should treat a null result as a
        /// 404 (never a 403), so no information leaks about paths outside the base directory.
        /// </summary>
        public static string? TryResolveMarkdownPath(string baseDir, string subFolder, string? slug)
        {
            if (!IsValidSlug(slug))
                return null;

            var baseFull = Path.GetFullPath(baseDir);
            var candidate = Path.GetFullPath(Path.Combine(baseFull, subFolder, slug + ".md"));

            var baseWithSeparator = baseFull.EndsWith(Path.DirectorySeparatorChar)
                ? baseFull
                : baseFull + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase))
                return null;

            return candidate;
        }
    }
}
