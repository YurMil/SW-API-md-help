using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SwApiHelp.Tests
{
    public class KnowledgeBaseTests
    {
        private static readonly string RepoRoot = FindRepoRoot();

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "README.md")) &&
                    File.Exists(Path.Combine(dir.FullName, ".gitignore")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            // Fallback to current directory
            return Directory.GetCurrentDirectory();
        }

        [Fact]
        public void Readme_ExistsAndHasSubstantialContent()
        {
            string readmePath = Path.Combine(RepoRoot, "README.md");
            Assert.True(File.Exists(readmePath), $"README.md not found at {readmePath}");

            string content = File.ReadAllText(readmePath);
            Assert.True(content.Length > 500, "README.md appears too short or empty.");
            Assert.Contains("SolidWorks API", content);
        }

        [Fact]
        public void Readme_AllMarkdownLinks_ResolveToExistingFiles()
        {
            string readmePath = Path.Combine(RepoRoot, "README.md");
            string content = File.ReadAllText(readmePath);

            // Matches markdown links: [Title](relative/path.md)
            var matches = Regex.Matches(content, @"\[(?<text>[^\]]+)\]\((?<url>[^)]+\.md)\)");
            Assert.NotEmpty(matches);

            var brokenLinks = new List<string>();

            foreach (Match match in matches)
            {
                string url = match.Groups["url"].Value;
                // Exclude external web links
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Clean up anchor fragments (#section)
                string filePath = url.Split('#')[0].Trim();
                filePath = filePath.Replace('/', Path.DirectorySeparatorChar);

                string fullPath = Path.GetFullPath(Path.Combine(RepoRoot, filePath));
                if (!File.Exists(fullPath))
                {
                    brokenLinks.Add($"Link '{url}' for [{match.Groups["text"].Value}] points to non-existent file: {fullPath}");
                }
            }

            Assert.True(brokenLinks.Count == 0,
                $"Found {brokenLinks.Count} broken link(s) in README.md:\n" + string.Join("\n", brokenLinks));
        }

        [Fact]
        public void Readme_IndexesAllArticlesFromNumberedFolders()
        {
            string readmePath = Path.Combine(RepoRoot, "README.md");
            string readmeContent = File.ReadAllText(readmePath);

            var allDocFiles = Directory.GetDirectories(RepoRoot)
                .Where(d => Path.GetFileName(d).Length > 0 && char.IsDigit(Path.GetFileName(d)[0]))
                .SelectMany(d => Directory.GetFiles(d, "*.md", SearchOption.AllDirectories))
                .Select(f => Path.GetRelativePath(RepoRoot, f).Replace('\\', '/'))
                .OrderBy(f => f)
                .ToList();

            Assert.NotEmpty(allDocFiles);

            var unindexedArticles = new List<string>();
            foreach (var docFile in allDocFiles)
            {
                if (!readmeContent.Contains(docFile, StringComparison.OrdinalIgnoreCase))
                {
                    unindexedArticles.Add(docFile);
                }
            }

            Assert.True(unindexedArticles.Count == 0,
                $"The following {unindexedArticles.Count} article(s) exist but are not linked in README.md:\n" +
                string.Join("\n", unindexedArticles));
        }

        [Fact]
        public void AllArticles_HaveValidHeadingsAndContent()
        {
            var allDocFiles = Directory.GetDirectories(RepoRoot)
                .Where(d => Path.GetFileName(d).Length > 0 && char.IsDigit(Path.GetFileName(d)[0]))
                .SelectMany(d => Directory.GetFiles(d, "*.md", SearchOption.AllDirectories))
                .ToList();

            var emptyOrHeaderlessFiles = new List<string>();

            foreach (var file in allDocFiles)
            {
                string text = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 100)
                {
                    emptyOrHeaderlessFiles.Add($"{file}: file is empty or too short");
                    continue;
                }

                // Check top-level H1 header
                if (!Regex.IsMatch(text, @"^#\s+.+", RegexOptions.Multiline))
                {
                    emptyOrHeaderlessFiles.Add($"{file}: missing H1 (# Heading)");
                }
            }

            Assert.True(emptyOrHeaderlessFiles.Count == 0,
                $"Article formatting issues:\n" + string.Join("\n", emptyOrHeaderlessFiles));
        }
    }
}
