using System;
using System.IO;
using System.Linq;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    /// <summary>
    /// Helper to build absolute paths to files in the Shared.API wwwroot/uploads folder.
    /// </summary>
    public static class FilePathHelper
    {
        /// <summary>
        /// Returns the absolute path to a file inside Shared.API/wwwroot/uploads.
        /// You can pass subfolders and filename as parameters.
        /// Example: GetApiUploadsPath("test", "sample1.pdf")
        /// </summary>
        /// <param name="relativePaths">Subfolders + filename relative to uploads folder</param>
        /// <returns>Absolute file path</returns>
        public static string GetApiUploadsPath(params string[] relativePaths)
        {
            // Base directory of the running application (usually bin/Debug/netX/)
            var baseDir = AppContext.BaseDirectory;

            // Path to API wwwroot/uploads folder
            var apiUploads = Path.Combine(baseDir, "..", "..", "..", "Shared.API", "wwwroot", "uploads");

            // Combine with additional relative paths (subfolders + filename)
            var fullPath = Path.GetFullPath(Path.Combine(new[] { apiUploads }.Concat(relativePaths).ToArray()));

            return fullPath;
        }
    }
}
