namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.IO;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.Metrics;
    using Xunit;

    public class OnnxEmbeddingProviderTests
    {
        private static readonly string ModelsDir = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "models", "unixcoder"));

        private static readonly string ModelPath = Path.Combine(ModelsDir, "model.onnx");
        private static readonly string VocabPath = Path.Combine(ModelsDir, "vocab.json");
        private static readonly string MergesPath = Path.Combine(ModelsDir, "merges.txt");

        private static bool ModelFilesExist =>
            File.Exists(ModelPath) && File.Exists(VocabPath) && File.Exists(MergesPath);

        [Fact]
        public void CanCreateProvider()
        {
            if (!ModelFilesExist) return;

            using var provider = OnnxEmbeddingProvider.Create(ModelPath, VocabPath, MergesPath);
            Assert.NotNull(provider);
        }

        [Fact]
        public async Task CanGenerateEmbeddings()
        {
            if (!ModelFilesExist) return;

            using var provider = OnnxEmbeddingProvider.Create(ModelPath, VocabPath, MergesPath);
            var embeddings = await provider.GetEmbeddings(new[] { "public void Hello() { Console.WriteLine(\"hi\"); }" });

            Assert.Single(embeddings);
            Assert.True(embeddings[0].Length > 0, "Embedding should have non-zero dimensions");
        }

        [Fact]
        public async Task SimilarCodeProducesSimilarEmbeddings()
        {
            if (!ModelFilesExist) return;

            using var provider = OnnxEmbeddingProvider.Create(ModelPath, VocabPath, MergesPath);
            var embeddings = await provider.GetEmbeddings(new[]
            {
                "public int Add(int a, int b) { return a + b; }",
                "public int Sum(int x, int y) { return x + y; }",
                "public string Reverse(string s) { return new string(s.Reverse().ToArray()); }"
            });

            var simAddSum = CosineSimilarity(embeddings[0], embeddings[1]);
            var simAddReverse = CosineSimilarity(embeddings[0], embeddings[2]);

            Assert.True(simAddSum > simAddReverse,
                $"Add/Sum similarity ({simAddSum:F4}) should be greater than Add/Reverse similarity ({simAddReverse:F4})");
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * (double)b[i];
                normA += a[i] * (double)a[i];
                normB += b[i] * (double)b[i];
            }
            return dot / (System.Math.Sqrt(normA) * System.Math.Sqrt(normB));
        }
    }
}
