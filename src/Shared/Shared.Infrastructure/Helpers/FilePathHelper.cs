using System;
using System.IO;
using System.Linq;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class FilePathHelper
    {
        /// <summary>
        /// Find the actual uploads folder for Shared.API/wwwroot/uploads by scanning upward
        /// from AppContext.BaseDirectory. Returns a full absolute path combined with
        /// any provided relative path segments.
        /// </summary>
        public static string GetApiUploadsPath(params string[] relativePaths)
        {
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);

                while (dir != null)
                {
                    // Try several candidate layouts relative to the current ancestor
                    var candidates = new[]
                    {
                        Path.Combine(dir.FullName, "src", "Shared", "Shared.API", "wwwroot", "uploads"),
                        Path.Combine(dir.FullName, "src", "Shared.API", "wwwroot", "uploads"),
                        Path.Combine(dir.FullName, "Shared.API", "wwwroot", "uploads"),
                        Path.Combine(dir.FullName, "wwwroot", "uploads")
                    };

                    foreach (var candidate in candidates)
                    {
                        if (Directory.Exists(candidate))
                        {
                            var final = Path.GetFullPath(Path.Combine(new[] { candidate }.Concat(relativePaths).ToArray()));
                            Console.WriteLine($"[FilePathHelper] Resolved uploads folder: {candidate}");
                            Console.WriteLine($"[FilePathHelper] Final path: {final}");
                            return final;
                        }
                    }

                    dir = dir.Parent;
                }

                // Fallback (previous heuristic) — but remove duplicate Shared.API\Shared.API if present
                var fallback = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Shared.API", "wwwroot", "uploads");
                var fallbackFull = Path.GetFullPath(fallback);

                // Normalize duplicate segments: e.g. ...\Shared.API\Shared.API\... -> ...\Shared.API\...
                var duplicate = Path.DirectorySeparatorChar + "Shared.API" + Path.DirectorySeparatorChar + "Shared.API" + Path.DirectorySeparatorChar;
                if (fallbackFull.Contains(duplicate))
                {
                    fallbackFull = fallbackFull.Replace(duplicate, Path.DirectorySeparatorChar + "Shared.API" + Path.DirectorySeparatorChar);
                }

                var fallbackResult = Path.GetFullPath(Path.Combine(new[] { fallbackFull }.Concat(relativePaths).ToArray()));
                Console.WriteLine($"[FilePathHelper] Using fallback uploads path: {fallbackFull}");
                Console.WriteLine($"[FilePathHelper] Final fallback path: {fallbackResult}");
                return fallbackResult;
            }
            catch (Exception ex)
            {
                // Last resort: return combined relative path from base directory
                var safe = Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "wwwroot", "uploads" }.Concat(relativePaths).ToArray()));
                Console.WriteLine($"[FilePathHelper] Error resolving uploads path: {ex.Message}. Returning safe path: {safe}");
                return safe;
            }
        }
    }
}
