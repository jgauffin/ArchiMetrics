namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;

    internal sealed class EmbeddingSimilarityAnalyzer
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly string _rootFolder;
        private readonly double _similarityThreshold;
        private readonly int _minimumTokens;

        public EmbeddingSimilarityAnalyzer(
            IEmbeddingProvider embeddingProvider,
            string rootFolder,
            double similarityThreshold = 0.85,
            int minimumTokens = 50)
        {
            _embeddingProvider = embeddingProvider;
            _rootFolder = rootFolder;
            _similarityThreshold = similarityThreshold;
            _minimumTokens = minimumTokens;
        }

        public async Task<IReadOnlyList<ClonePair>> Analyze(
            IEnumerable<SyntaxTree> trees,
            ISet<string> alreadyDetectedKeys,
            CancellationToken cancellationToken = default)
        {
            var extractor = new MethodExtractor(_rootFolder, _minimumTokens);
            var instances = extractor.Extract(trees);

            if (instances.Count < 2)
            {
                return Array.Empty<ClonePair>();
            }

            var texts = instances.Select(i => i.NormalizedText).ToList();
            var embeddings = await _embeddingProvider.GetEmbeddings(texts, cancellationToken).ConfigureAwait(false);

            var pairs = new List<ClonePair>();

            for (var i = 0; i < instances.Count; i++)
            {
                for (var j = i + 1; j < instances.Count; j++)
                {
                    var key = MakePairKey(instances[i], instances[j]);
                    if (alreadyDetectedKeys.Contains(key))
                    {
                        continue;
                    }

                    var similarity = CosineSimilarity(embeddings[i], embeddings[j]);
                    if (similarity >= _similarityThreshold)
                    {
                        pairs.Add(new ClonePair(instances[i], instances[j], CloneType.Semantic, similarity));
                    }
                }
            }

            return pairs;
        }

        internal static string MakePairKey(CloneInstance a, CloneInstance b)
        {
            var left = $"{a.FilePath}:{a.LineNumber}";
            var right = $"{b.FilePath}:{b.LineNumber}";
            return string.Compare(left, right, StringComparison.Ordinal) < 0
                ? left + "|" + right
                : right + "|" + left;
        }

        internal static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
            {
                return 0.0;
            }

            double dot = 0, normA = 0, normB = 0;
            for (var i = 0; i < a.Length; i++)
            {
                dot += a[i] * (double)b[i];
                normA += a[i] * (double)a[i];
                normB += b[i] * (double)b[i];
            }

            var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denom == 0 ? 0.0 : dot / denom;
        }
    }
}
