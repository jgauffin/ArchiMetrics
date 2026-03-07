namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;

    internal sealed class SyntaxFingerprintAnalyzer
    {
        private readonly string _rootFolder;
        private readonly int _minimumTokens;

        public SyntaxFingerprintAnalyzer(string rootFolder, int minimumTokens = 50)
        {
            _rootFolder = rootFolder;
            _minimumTokens = minimumTokens;
        }

        public IReadOnlyList<CloneClass> Analyze(IEnumerable<SyntaxTree> trees)
        {
            var extractor = new MethodExtractor(_rootFolder, _minimumTokens);
            var instances = extractor.Extract(trees);

            var groups = instances
                .GroupBy(i => ComputeHash(i.NormalizedText))
                .Where(g => g.Count() > 1)
                .ToList();

            return groups
                .Select(g => new CloneClass(
                    DetermineCloneType(g),
                    g.ToList(),
                    1.0))
                .ToList();
        }

        private static CloneType DetermineCloneType(IGrouping<string, CloneInstance> group)
        {
            // All items in a hash group have identical normalized form.
            // Since normalization replaces identifiers with ID, these are at least Type-2 (renamed).
            // Could be Type-1 (exact) if original text is also identical — but we don't track raw text.
            return CloneType.Renamed;
        }

        private static string ComputeHash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
