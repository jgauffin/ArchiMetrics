namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.Metrics;
    using Microsoft.ML.OnnxRuntime;
    using Microsoft.ML.OnnxRuntime.Tensors;
    using Microsoft.ML.Tokenizers;

    public sealed class OnnxEmbeddingProvider : IEmbeddingProvider, IDisposable
    {
        private readonly InferenceSession _session;
        private readonly BpeTokenizer _tokenizer;
        private readonly int _maxSequenceLength;

        private OnnxEmbeddingProvider(InferenceSession session, BpeTokenizer tokenizer, int maxSequenceLength)
        {
            _session = session;
            _tokenizer = tokenizer;
            _maxSequenceLength = maxSequenceLength;
        }

        public static OnnxEmbeddingProvider Create(string modelPath, string vocabPath, string mergesPath, int maxSequenceLength = 512)
        {
            var session = new InferenceSession(modelPath);

            using var vocabStream = File.OpenRead(vocabPath);
            using var mergesStream = File.OpenRead(mergesPath);
            var tokenizer = BpeTokenizer.Create(vocabStream, mergesStream, unknownToken: "<unk>");

            return new OnnxEmbeddingProvider(session, tokenizer, maxSequenceLength);
        }

        public Task<IReadOnlyList<float[]>> GetEmbeddings(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            var results = new List<float[]>(texts.Count);

            foreach (var text in texts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var embedding = GetSingleEmbedding(text);
                results.Add(embedding);
            }

            return Task.FromResult<IReadOnlyList<float[]>>(results);
        }

        private float[] GetSingleEmbedding(string text)
        {
            var tokenIds = _tokenizer.EncodeToIds(text, _maxSequenceLength, out _, out _);
            var length = tokenIds.Count;

            var inputIdsTensor = new DenseTensor<long>(new[] { 1, length });
            var attentionMaskTensor = new DenseTensor<long>(new[] { 1, length });

            for (var i = 0; i < length; i++)
            {
                inputIdsTensor[0, i] = tokenIds[i];
                attentionMaskTensor[0, i] = 1;
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            using var output = _session.Run(inputs);
            var lastHiddenState = output.First().AsTensor<float>();

            return MeanPool(lastHiddenState, length);
        }

        private static float[] MeanPool(Tensor<float> hiddenState, int tokenCount)
        {
            var hiddenSize = hiddenState.Dimensions[2];
            var embedding = new float[hiddenSize];

            for (var t = 0; t < tokenCount; t++)
            {
                for (var h = 0; h < hiddenSize; h++)
                {
                    embedding[h] += hiddenState[0, t, h];
                }
            }

            for (var h = 0; h < hiddenSize; h++)
            {
                embedding[h] /= tokenCount;
            }

            // L2 normalize
            var norm = 0.0;
            for (var h = 0; h < hiddenSize; h++)
            {
                norm += embedding[h] * (double)embedding[h];
            }

            norm = Math.Sqrt(norm);
            if (norm > 0)
            {
                for (var h = 0; h < hiddenSize; h++)
                {
                    embedding[h] = (float)(embedding[h] / norm);
                }
            }

            return embedding;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
