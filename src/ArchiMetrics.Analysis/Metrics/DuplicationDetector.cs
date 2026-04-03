namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;

    public sealed class DuplicationDetector
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly string _rootFolder;
        private readonly int _minimumTokens;
        private readonly double _similarityThreshold;
        private readonly int _maxClusterSize;

        public DuplicationDetector(
            string rootFolder,
            IEmbeddingProvider embeddingProvider = null,
            int minimumTokens = 50,
            double similarityThreshold = 0.85,
            int maxClusterSize = 15)
        {
            _rootFolder = rootFolder;
            _embeddingProvider = embeddingProvider;
            _minimumTokens = minimumTokens;
            _similarityThreshold = similarityThreshold;
            _maxClusterSize = maxClusterSize;
        }

        public async Task<DuplicationResult> Detect(
            IEnumerable<SyntaxTree> trees,
            CancellationToken cancellationToken = default)
        {
            // Extract methods once — both layers share the same instances
            var extractor = new MethodExtractor(_rootFolder, _minimumTokens);
            var instances = extractor.Extract(trees);

            // Layer 1: AST fingerprinting — fast, exact/renamed clones
            var fingerprinter = new SyntaxFingerprintAnalyzer();
            var astClones = fingerprinter.Analyze(instances);

            if (_embeddingProvider == null)
            {
                return new DuplicationResult(astClones);
            }

            // Build set of already-detected pairs so Layer 2 skips them
            var detectedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clone in astClones)
            {
                for (var i = 0; i < clone.Instances.Count; i++)
                {
                    for (var j = i + 1; j < clone.Instances.Count; j++)
                    {
                        detectedKeys.Add(EmbeddingSimilarityAnalyzer.MakePairKey(
                            clone.Instances[i], clone.Instances[j]));
                    }
                }
            }

            // Layer 2: Embedding similarity — catches semantic clones
            var embeddingAnalyzer = new EmbeddingSimilarityAnalyzer(
                _embeddingProvider, _similarityThreshold);
            var semanticPairs = await embeddingAnalyzer
                .Analyze(instances, detectedKeys, cancellationToken)
                .ConfigureAwait(false);

            // Group semantic pairs into clone classes by connected components
            var semanticClones = GroupIntoClusters(semanticPairs, _maxClusterSize);

            var allClones = astClones.Concat(semanticClones).ToList();
            return new DuplicationResult(allClones);
        }

        internal static IReadOnlyList<CloneClass> GroupIntoClusters(IReadOnlyList<ClonePair> pairs, int maxClusterSize = 15)
        {
            if (pairs.Count == 0)
            {
                return Array.Empty<CloneClass>();
            }

            // Union-Find to group connected instances
            var instanceMap = new Dictionary<string, CloneInstance>();
            var parent = new Dictionary<string, string>();

            foreach (var pair in pairs)
            {
                var leftKey = Key(pair.Left);
                var rightKey = Key(pair.Right);
                instanceMap[leftKey] = pair.Left;
                instanceMap[rightKey] = pair.Right;
                if (!parent.ContainsKey(leftKey)) parent[leftKey] = leftKey;
                if (!parent.ContainsKey(rightKey)) parent[rightKey] = rightKey;
                Union(parent, leftKey, rightKey);
            }

            // Pre-build root → pairs mapping in one pass (avoids O(G*P) re-scanning)
            var pairsByRoot = new Dictionary<string, List<ClonePair>>();
            foreach (var pair in pairs)
            {
                var root = Find(parent, Key(pair.Left));
                if (!pairsByRoot.TryGetValue(root, out var list))
                {
                    list = new List<ClonePair>();
                    pairsByRoot[root] = list;
                }
                list.Add(pair);
            }

            var groups = instanceMap.Keys
                .GroupBy(k => Find(parent, k))
                .ToList();

            var result = new List<CloneClass>();
            foreach (var group in groups)
            {
                var instances = group.Select(k => instanceMap[k]).ToList();
                if (instances.Count < 2 || instances.Count > maxClusterSize) continue;

                var root = group.Key;
                var avgSimilarity = pairsByRoot.TryGetValue(root, out var clusterPairs)
                    ? clusterPairs.Average(p => p.Similarity)
                    : 0.0;

                result.Add(new CloneClass(CloneType.Semantic, instances, avgSimilarity));
            }

            return result;
        }

        private static string Key(CloneInstance instance) =>
            $"{instance.FilePath}:{instance.LineNumber}";

        private static string Find(Dictionary<string, string> parent, string x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        private static void Union(Dictionary<string, string> parent, string a, string b)
        {
            var ra = Find(parent, a);
            var rb = Find(parent, b);
            if (ra != rb)
            {
                parent[ra] = rb;
            }
        }
    }
}
