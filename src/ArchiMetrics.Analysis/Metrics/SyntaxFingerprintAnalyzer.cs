namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Common.Metrics;

    internal sealed class SyntaxFingerprintAnalyzer
    {

        public IReadOnlyList<CloneClass> Analyze(IReadOnlyList<CloneInstance> instances)
        {
            var groups = instances
                .GroupBy(i => ComputeHash(i.NormalizedText))
                .Where(g => g.Skip(1).Any())
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
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Convert.ToBase64String(bytes);
        }
    }
}
