namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.Common.Metrics;
    using ArchiMetrics.Analysis.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Xunit;

    public sealed class DuplicationDetectorTests
    {
        public class GivenAstFingerprinting
        {
            [Fact]
            public async Task WhenTwoMethodsAreIdenticalThenDetectsClone()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Calculate(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i * 2;
                if (result > 100)
                {
                    result = result / 2;
                }
            }
            return result;
        }
    }

    public class B
    {
        public int Calculate(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i * 2;
                if (result > 100)
                {
                    result = result / 2;
                }
            }
            return result;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var detector = new DuplicationDetector(string.Empty, minimumTokens: 10);

                var result = await detector.Detect(new[] { tree });

                Assert.NotEmpty(result.Clones);
                Assert.Equal(2, result.Clones[0].Instances.Count);
            }

            [Fact]
            public async Task WhenMethodsHaveRenamedVariablesThenDetectsClone()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo(int input)
        {
            var total = 0;
            for (var idx = 0; idx < input; idx++)
            {
                total += idx * 2;
                if (total > 100)
                {
                    total = total / 2;
                }
            }
            return total;
        }
    }

    public class B
    {
        public int Bar(int count)
        {
            var sum = 0;
            for (var j = 0; j < count; j++)
            {
                sum += j * 2;
                if (sum > 100)
                {
                    sum = sum / 2;
                }
            }
            return sum;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var detector = new DuplicationDetector(string.Empty, minimumTokens: 10);

                var result = await detector.Detect(new[] { tree });

                Assert.NotEmpty(result.Clones);
                Assert.Equal(CloneType.Renamed, result.Clones[0].CloneType);
            }

            [Fact]
            public async Task WhenMethodsAreDifferentThenNoClone()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo(int x)
        {
            return x + 1;
        }
    }

    public class B
    {
        public string Bar(string s)
        {
            return s.ToUpper().Trim().Replace(""a"", ""b"");
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var detector = new DuplicationDetector(string.Empty, minimumTokens: 5);

                var result = await detector.Detect(new[] { tree });

                Assert.Empty(result.Clones);
            }

            [Fact]
            public async Task WhenMethodsTooSmallThenSkipped()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo() { return 1; }
    }

    public class B
    {
        public int Bar() { return 1; }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var detector = new DuplicationDetector(string.Empty, minimumTokens: 50);

                var result = await detector.Detect(new[] { tree });

                Assert.Empty(result.Clones);
            }
        }

        public class GivenEmbeddingLayer
        {
            [Fact]
            public async Task WhenEmbeddingsAreSimilarThenDetectsSemanticClone()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i;
            }
            return result;
        }
    }

    public class B
    {
        public int Bar(int x)
        {
            var total = 0;
            var counter = 0;
            while (counter < x)
            {
                total = total + counter;
                counter++;
            }
            return total;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var fakeProvider = new FakeEmbeddingProvider(similarity: 0.95);
                var detector = new DuplicationDetector(
                    string.Empty,
                    embeddingProvider: fakeProvider,
                    minimumTokens: 10,
                    similarityThreshold: 0.80);

                var result = await detector.Detect(new[] { tree });

                Assert.True(result.Clones.Any(c => c.CloneType == CloneType.Semantic));
            }

            [Fact]
            public async Task WhenEmbeddingsAreDissimilarThenNoSemanticClone()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i;
            }
            return result;
        }
    }

    public class B
    {
        public string Bar(string s)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var c in s)
            {
                builder.Append(char.ToUpper(c));
            }
            return builder.ToString();
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var fakeProvider = new FakeEmbeddingProvider(similarity: 0.3);
                var detector = new DuplicationDetector(
                    string.Empty,
                    embeddingProvider: fakeProvider,
                    minimumTokens: 10,
                    similarityThreshold: 0.80);

                var result = await detector.Detect(new[] { tree });

                Assert.DoesNotContain(result.Clones, c => c.CloneType == CloneType.Semantic);
            }
        }

        public class GroupIntoClustersTests
        {
            [Fact]
            public void SmallClusterIsRetained()
            {
                var a = new CloneInstance("a.cs", 1, 10, "Foo", "body");
                var b = new CloneInstance("b.cs", 1, 10, "Bar", "body");
                var c = new CloneInstance("c.cs", 1, 10, "Baz", "body");

                var pairs = new List<ClonePair>
                {
                    new ClonePair(a, b, CloneType.Semantic, 0.92),
                    new ClonePair(b, c, CloneType.Semantic, 0.90),
                };

                var result = DuplicationDetector.GroupIntoClusters(pairs, maxClusterSize: 15);

                Assert.Single(result);
                Assert.Equal(3, result[0].Instances.Count);
            }

            [Fact]
            public void OversizedClusterIsFiltered()
            {
                // Create 20 instances forming one big connected component
                var instances = Enumerable.Range(0, 20)
                    .Select(i => new CloneInstance($"file{i}.cs", 1, 10, $"Method{i}", "body"))
                    .ToList();

                var pairs = new List<ClonePair>();
                for (var i = 0; i < instances.Count - 1; i++)
                {
                    pairs.Add(new ClonePair(instances[i], instances[i + 1], CloneType.Semantic, 0.96));
                }

                var result = DuplicationDetector.GroupIntoClusters(pairs, maxClusterSize: 15);

                Assert.Empty(result);
            }

            [Fact]
            public void MixedClustersOnlyFiltersOversized()
            {
                // Small cluster of 3
                var a = new CloneInstance("a.cs", 1, 10, "A", "body");
                var b = new CloneInstance("b.cs", 1, 10, "B", "body");
                var c = new CloneInstance("c.cs", 1, 10, "C", "body");

                // Large cluster of 5 (will exceed maxClusterSize=4)
                var d = new CloneInstance("d.cs", 1, 10, "D", "body");
                var e = new CloneInstance("e.cs", 1, 10, "E", "body");
                var f = new CloneInstance("f.cs", 1, 10, "F", "body");
                var g = new CloneInstance("g.cs", 1, 10, "G", "body");
                var h = new CloneInstance("h.cs", 1, 10, "H", "body");

                var pairs = new List<ClonePair>
                {
                    // Small cluster
                    new ClonePair(a, b, CloneType.Semantic, 0.92),
                    new ClonePair(b, c, CloneType.Semantic, 0.90),
                    // Large cluster
                    new ClonePair(d, e, CloneType.Semantic, 0.96),
                    new ClonePair(e, f, CloneType.Semantic, 0.95),
                    new ClonePair(f, g, CloneType.Semantic, 0.94),
                    new ClonePair(g, h, CloneType.Semantic, 0.93),
                };

                var result = DuplicationDetector.GroupIntoClusters(pairs, maxClusterSize: 4);

                Assert.Single(result);
                Assert.Equal(3, result[0].Instances.Count);
            }

            [Fact]
            public void EmptyPairsReturnsEmpty()
            {
                var result = DuplicationDetector.GroupIntoClusters(new List<ClonePair>());
                Assert.Empty(result);
            }
        }

        public class CosineSimilarityTests
        {
            [Fact]
            public void IdenticalVectorsReturnOne()
            {
                var a = new float[] { 1, 2, 3 };
                var similarity = EmbeddingSimilarityAnalyzer.CosineSimilarity(a, a);
                Assert.Equal(1.0, similarity, 5);
            }

            [Fact]
            public void OrthogonalVectorsReturnZero()
            {
                var a = new float[] { 1, 0, 0 };
                var b = new float[] { 0, 1, 0 };
                var similarity = EmbeddingSimilarityAnalyzer.CosineSimilarity(a, b);
                Assert.Equal(0.0, similarity, 5);
            }

            [Fact]
            public void EmptyVectorsReturnZero()
            {
                var similarity = EmbeddingSimilarityAnalyzer.CosineSimilarity(
                    new float[0], new float[0]);
                Assert.Equal(0.0, similarity);
            }
        }

        public class SyntaxNormalizerTests
        {
            [Fact]
            public void NormalizesIdentifiersToSameToken()
            {
                var code1 = "{ var foo = 1; return foo; }";
                var code2 = "{ var bar = 1; return bar; }";

                var tree1 = CSharpSyntaxTree.ParseText(code1, new CSharpParseOptions(kind: SourceCodeKind.Script));
                var tree2 = CSharpSyntaxTree.ParseText(code2, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var norm1 = SyntaxNormalizer.Normalize(tree1.GetRoot());
                var norm2 = SyntaxNormalizer.Normalize(tree2.GetRoot());

                Assert.Equal(norm1, norm2);
            }
        }

        private class FakeEmbeddingProvider : IEmbeddingProvider
        {
            private readonly double _similarity;

            public FakeEmbeddingProvider(double similarity)
            {
                _similarity = similarity;
            }

            public Task<IReadOnlyList<float[]>> GetEmbeddings(
                IReadOnlyList<string> texts,
                CancellationToken cancellationToken = default)
            {
                // Generate embeddings where consecutive pairs have the requested similarity.
                // Use a simple approach: first vector is [1,0,...,0], subsequent vectors are
                // rotated to achieve the desired cosine similarity.
                var dim = 64;
                var result = new List<float[]>();

                for (var i = 0; i < texts.Count; i++)
                {
                    var vec = new float[dim];
                    if (i == 0)
                    {
                        vec[0] = 1.0f;
                    }
                    else
                    {
                        // cos(theta) = similarity, so the vector is [similarity, sqrt(1-sim^2), 0, ...]
                        vec[0] = (float)_similarity;
                        vec[i % (dim - 1) + 1] = (float)System.Math.Sqrt(1.0 - _similarity * _similarity);
                    }

                    result.Add(vec);
                }

                return Task.FromResult<IReadOnlyList<float[]>>(result);
            }
        }
    }
}
