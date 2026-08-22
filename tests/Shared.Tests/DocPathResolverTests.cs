using shop_back.src.Shared.Infrastructure.Helpers;
using Xunit;

namespace Shared.Tests
{
    public class DocPathResolverTests
    {
        private static string CreateTempDocsRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "doc-path-resolver-tests-" + Guid.NewGuid());
            var developerGuideDir = Path.Combine(root, "developer-guide", "shop_back");
            Directory.CreateDirectory(developerGuideDir);
            File.WriteAllText(Path.Combine(developerGuideDir, "overview.md"), "# Overview\n\nHello.");

            var userGuidesDir = Path.Combine(root, "user-guides");
            Directory.CreateDirectory(userGuidesDir);
            File.WriteAllText(Path.Combine(userGuidesDir, "developer.md"), "# Developer User Guide\n\nHello.");

            return root;
        }

        [Fact]
        public void ValidSlug_ResolvesToFileInsideBaseDirectory()
        {
            var baseDir = CreateTempDocsRoot();

            var resolved = DocPathResolver.TryResolveMarkdownPath(baseDir, "developer-guide", "shop_back/overview");

            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved));
            var baseFull = Path.GetFullPath(baseDir);
            Assert.StartsWith(baseFull + Path.DirectorySeparatorChar, resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("../secrets")]
        [InlineData("..\\secrets")]
        [InlineData("shop_back/../../../secrets")]
        [InlineData("./../secrets")]
        public void DotDotSlug_IsRejected(string maliciousSlug)
        {
            var baseDir = CreateTempDocsRoot();

            var resolved = DocPathResolver.TryResolveMarkdownPath(baseDir, "developer-guide", maliciousSlug);

            Assert.Null(resolved);
        }

        [Theory]
        [InlineData("C:/Windows/win.ini")]
        [InlineData("/etc/passwd")]
        [InlineData("C:\\Windows\\win.ini")]
        public void AbsolutePathSlug_IsRejected(string maliciousSlug)
        {
            var baseDir = CreateTempDocsRoot();

            var resolved = DocPathResolver.TryResolveMarkdownPath(baseDir, "developer-guide", maliciousSlug);

            Assert.Null(resolved);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("weird*chars?")]
        public void InvalidOrEmptySlug_FailsAllowlistCheck(string? slug)
        {
            Assert.False(DocPathResolver.IsValidSlug(slug));
            Assert.Null(DocPathResolver.TryResolveMarkdownPath(CreateTempDocsRoot(), "developer-guide", slug));
        }

        [Fact]
        public void ValidSlug_PassesAllowlistCheck()
        {
            Assert.True(DocPathResolver.IsValidSlug("shop_back/overview"));
            Assert.True(DocPathResolver.IsValidSlug("developer"));
        }

        [Fact]
        public void UserGuideSlug_FromRoleName_ResolvesWithinUserGuidesFolder()
        {
            var baseDir = CreateTempDocsRoot();

            var resolved = DocPathResolver.TryResolveMarkdownPath(baseDir, "user-guides", "developer");

            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved));
        }
    }
}
