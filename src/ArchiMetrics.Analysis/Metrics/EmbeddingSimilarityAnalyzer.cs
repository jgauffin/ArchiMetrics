namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.Metrics;

    internal sealed class EmbeddingSimilarityAnalyzer
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly double _similarityThreshold;

        public EmbeddingSimilarityAnalyzer(
            IEmbeddingProvider embeddingProvider,
            double similarityThreshold = 0.85)
        {
            _embeddingProvider = embeddingProvider;
            _similarityThreshold = similarityThreshold;
        }

        public async Task<IReadOnlyList<ClonePair>> Analyze(
            IReadOnlyList<CloneInstance> instances,
            ISet<string> alreadyDetectedKeys,
            CancellationToken cancellationToken = default)
        {
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
            // Compare file paths first, then line numbers to establish canonical order
            var cmp = string.Compare(a.FilePath, b.FilePath, StringComparison.Ordinal);
            if (cmp == 0) cmp = a.LineNumber.CompareTo(b.LineNumber);

            return cmp < 0
                ? $"{a.FilePath}:{a.LineNumber}|{b.FilePath}:{b.LineNumber}"
                : $"{b.FilePath}:{b.LineNumber}|{a.FilePath}:{a.LineNumber}";
        }

        internal static double CosineSimilarity(float[] a, float[] b) =>
            VectorMath.CosineSimilarity(a, b);
    }
}
