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

        public DuplicationDetector(
            string rootFolder,
            IEmbeddingProvider embeddingProvider = null,
            int minimumTokens = 50,
            double similarityThreshold = 0.85)
        {
            _rootFolder = rootFolder;
            _embeddingProvider = embeddingProvider;
            _minimumTokens = minimumTokens;
            _similarityThreshold = similarityThreshold;
        }

        public async Task<DuplicationResult> Detect(
            IEnumerable<SyntaxTree> trees,
            CancellationToken cancellationToken = default)
        {
            var treeList = trees.ToList();

            // Layer 1: AST fingerprinting — fast, exact/renamed clones
            var fingerprinter = new SyntaxFingerprintAnalyzer(_rootFolder, _minimumTokens);
            var astClones = fingerprinter.Analyze(treeList);

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
                _embeddingProvider, _rootFolder, _similarityThreshold, _minimumTokens);
            var semanticPairs = await embeddingAnalyzer
                .Analyze(treeList, detectedKeys, cancellationToken)
                .ConfigureAwait(false);

            // Group semantic pairs into clone classes by connected components
            var semanticClones = GroupIntoClusters(semanticPairs);

            var allClones = astClones.Concat(semanticClones).ToList();
            return new DuplicationResult(allClones);
        }

        private static IReadOnlyList<CloneClass> GroupIntoClusters(IReadOnlyList<ClonePair> pairs)
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

            var groups = instanceMap.Keys
                .GroupBy(k => Find(parent, k))
                .ToList();

            var result = new List<CloneClass>();
            foreach (var group in groups)
            {
                var instances = group.Select(k => instanceMap[k]).ToList();
                if (instances.Count < 2) continue;

                var avgSimilarity = pairs
                    .Where(p => Find(parent, Key(p.Left)) == Find(parent, Key(p.Right)))
                    .Average(p => p.Similarity);

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
