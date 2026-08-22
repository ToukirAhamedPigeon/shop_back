// src/Shared/Shared.Infrastructure/Services/DocumentationService.cs
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using shop_back.src.Shared.Application.DTOs.Documentation;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.Settings;
using shop_back.src.Shared.Infrastructure.Helpers;
using StackExchange.Redis;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class DocumentationService : IDocumentationService
    {
        private const string DeveloperGuideFolder = "developer-guide";
        private const string UserGuidesFolder = "user-guides";
        private const string DevTreeCacheKey = "Documentation:DevTree";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _baseDir;
        private readonly IDatabase? _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

        // IConnectionMultiplexer is registered as a singleton only when Redis is reachable at
        // startup (see Program.cs). Documentation is a low-stakes, filesystem-backed feature, so
        // rather than tying its availability to Redis being up (as several other services in this
        // codebase implicitly do), the dependency here is optional: the default parameter value
        // lets the DI container fall back to null when IConnectionMultiplexer isn't registered,
        // and every cache access below is guarded accordingly.
        public DocumentationService(IOptions<DocumentationSettings> options, IConnectionMultiplexer? redis = null)
        {
            var rootPath = options.Value?.RootPath;
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new InvalidOperationException("Documentation:RootPath is not configured.");

            _baseDir = Path.GetFullPath(rootPath);
            _cache = redis?.GetDatabase();
        }

        public async Task<List<DocTreeNodeDto>> GetDeveloperTreeAsync()
        {
            if (_cache != null)
            {
                var cached = await _cache.StringGetAsync(DevTreeCacheKey);
                if (cached.HasValue)
                {
                    var cachedTree = JsonSerializer.Deserialize<List<DocTreeNodeDto>>(cached!, JsonOptions);
                    if (cachedTree != null)
                        return cachedTree;
                }
            }

            var root = Path.Combine(_baseDir, DeveloperGuideFolder);
            var tree = Directory.Exists(root) ? BuildTree(root, root) : new List<DocTreeNodeDto>();

            if (_cache != null)
            {
                await _cache.StringSetAsync(DevTreeCacheKey, JsonSerializer.Serialize(tree, JsonOptions), _cacheTtl);
            }

            return tree;
        }

        public async Task<DevDocPageDto?> GetDeveloperPageAsync(string? slug)
        {
            var resolvedPath = DocPathResolver.TryResolveMarkdownPath(_baseDir, DeveloperGuideFolder, slug);
            if (resolvedPath == null || !File.Exists(resolvedPath))
                return null;

            var markdown = await File.ReadAllTextAsync(resolvedPath);

            return new DevDocPageDto
            {
                Title = ExtractTitle(markdown) ?? Path.GetFileNameWithoutExtension(resolvedPath),
                Markdown = markdown,
                SourcePaths = new List<string>(),
                UpdatedAt = File.GetLastWriteTimeUtc(resolvedPath)
            };
        }

        public async Task<UserGuideDto> GetUserGuideAsync(string? roleName)
        {
            var displayRole = string.IsNullOrWhiteSpace(roleName) ? "User" : roleName;
            var slug = NormalizeRoleSlug(roleName);

            var resolvedPath = slug != null
                ? DocPathResolver.TryResolveMarkdownPath(_baseDir, UserGuidesFolder, slug)
                : null;

            if (resolvedPath != null && File.Exists(resolvedPath))
            {
                var markdown = await File.ReadAllTextAsync(resolvedPath);
                return new UserGuideDto
                {
                    Title = ExtractTitle(markdown) ?? $"{displayRole} User Guide",
                    Markdown = markdown,
                    Role = displayRole,
                    UpdatedAt = File.GetLastWriteTimeUtc(resolvedPath)
                };
            }

            // Expected before the first /generate-doc run (or if the role slug could not be
            // resolved safely) — return 200 with a placeholder rather than failing hard.
            return new UserGuideDto
            {
                Title = $"{displayRole} User Guide",
                Markdown = $"# {displayRole} User Guide\n\nThis guide has not been generated yet. Run `/generate-doc`.",
                Role = displayRole,
                UpdatedAt = null
            };
        }

        public async Task<PagedChangelogDto> GetChangelogAsync(int page, int pageSize, string? repo)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 200);

            var changelogPath = Path.Combine(_baseDir, "_meta", "changelog.json");

            var entries = new List<ChangelogEntryDto>();
            if (File.Exists(changelogPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(changelogPath);
                    var document = JsonSerializer.Deserialize<ChangelogFileDto>(json, JsonOptions);
                    entries = document?.Entries ?? new List<ChangelogEntryDto>();
                }
                catch (JsonException)
                {
                    entries = new List<ChangelogEntryDto>();
                }
            }

            if (!string.IsNullOrWhiteSpace(repo))
            {
                entries = entries.Where(e => string.Equals(e.Repo, repo, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var totalCount = entries.Count;
            var items = entries.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedChangelogDto
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private static List<DocTreeNodeDto> BuildTree(string baseDir, string currentDir)
        {
            var nodes = new List<DocTreeNodeDto>();

            foreach (var dir in Directory.GetDirectories(currentDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var children = BuildTree(baseDir, dir);
                if (children.Count == 0)
                    continue; // skip folders with no markdown content

                nodes.Add(new DocTreeNodeDto
                {
                    Name = Path.GetFileName(dir),
                    Slug = ToSlug(baseDir, dir),
                    IsFile = false,
                    Children = children
                });
            }

            foreach (var file in Directory.GetFiles(currentDir, "*.md").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new DocTreeNodeDto
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Slug = ToSlug(baseDir, file, stripExtension: true),
                    IsFile = true,
                    Children = new List<DocTreeNodeDto>()
                });
            }

            return nodes;
        }

        private static string ToSlug(string baseDir, string fullPath, bool stripExtension = false)
        {
            var relative = Path.GetRelativePath(baseDir, fullPath);
            if (stripExtension)
                relative = Path.ChangeExtension(relative, null);

            return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string? ExtractTitle(string markdown)
        {
            using var reader = new StringReader(markdown);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("# "))
                    return trimmed[2..].Trim();
            }
            return null;
        }

        // Role names come from the app's own Role CRUD (created by whoever holds role-management
        // permission), so they are attacker-influenceable and MUST go through the same allowlist
        // as any other slug before touching the filesystem — never special-cased as "safe".
        private static string? NormalizeRoleSlug(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return null;

            var slug = Regex.Replace(roleName.Trim().ToLowerInvariant(), @"\s+", "-");
            return DocPathResolver.IsValidSlug(slug) ? slug : null;
        }

        private class ChangelogFileDto
        {
            public int SchemaVersion { get; set; }
            public List<ChangelogEntryDto>? Entries { get; set; }
        }
    }
}
