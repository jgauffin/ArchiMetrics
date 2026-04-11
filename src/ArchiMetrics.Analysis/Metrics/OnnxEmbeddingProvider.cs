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
        private readonly int _batchSize;

        private OnnxEmbeddingProvider(InferenceSession session, BpeTokenizer tokenizer, int maxSequenceLength, int batchSize)
        {
            _session = session;
            _tokenizer = tokenizer;
            _maxSequenceLength = maxSequenceLength;
            _batchSize = batchSize;
        }

        public static OnnxEmbeddingProvider Create(string modelPath, string vocabPath, string mergesPath, int maxSequenceLength = 512, int batchSize = 64)
        {
            var session = new InferenceSession(modelPath);

            using var vocabStream = File.OpenRead(vocabPath);
            using var mergesStream = File.OpenRead(mergesPath);
            var tokenizer = BpeTokenizer.Create(vocabStream, mergesStream, unknownToken: "<unk>");

            return new OnnxEmbeddingProvider(session, tokenizer, maxSequenceLength, batchSize);
        }

        public Task<IReadOnlyList<float[]>> GetEmbeddings(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (texts.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());
            }

            if (texts.Count == 1)
            {
                return Task.FromResult<IReadOnlyList<float[]>>(new[] { GetSingleEmbedding(texts[0]) });
            }

            var results = new float[texts.Count][];

            // Tokenize all texts upfront and track original indices
            var tokenized = new (int OriginalIndex, IReadOnlyList<int> TokenIds)[texts.Count];
            for (var i = 0; i < texts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tokenized[i] = (i, _tokenizer.EncodeToIds(texts[i], _maxSequenceLength, out _, out _));
            }

            // Sort by token count so each batch has minimal padding waste
            Array.Sort(tokenized, (a, b) => a.TokenIds.Count.CompareTo(b.TokenIds.Count));

            // Process in batches — one ONNX inference per batch instead of per text
            for (var batchStart = 0; batchStart < tokenized.Length; batchStart += _batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batchEnd = Math.Min(batchStart + _batchSize, tokenized.Length);
                var batchCount = batchEnd - batchStart;
                var maxLen = tokenized[batchEnd - 1].TokenIds.Count;

                var inputIds = new DenseTensor<long>(new[] { batchCount, maxLen });
                var attentionMask = new DenseTensor<long>(new[] { batchCount, maxLen });

                for (var b = 0; b < batchCount; b++)
                {
                    var ids = tokenized[batchStart + b].TokenIds;
                    for (var t = 0; t < ids.Count; t++)
                    {
                        inputIds[b, t] = ids[t];
                        attentionMask[b, t] = 1;
                    }
                    // Padding positions stay 0 (DenseTensor default)
                }

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                    NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
                };

                using var output = _session.Run(inputs);
                var hiddenState = output.First().AsTensor<float>();
                var hiddenSize = hiddenState.Dimensions[2];

                for (var b = 0; b < batchCount; b++)
                {
                    var tokenCount = tokenized[batchStart + b].TokenIds.Count;
                    var origIndex = tokenized[batchStart + b].OriginalIndex;
                    results[origIndex] = MeanPool(hiddenState, b, tokenCount, hiddenSize);
                }
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
            var hiddenSize = lastHiddenState.Dimensions[2];

            return MeanPool(lastHiddenState, 0, length, hiddenSize);
        }

        private static float[] MeanPool(Tensor<float> hiddenState, int batchIndex, int tokenCount, int hiddenSize)
        {
            var embedding = new float[hiddenSize];

            for (var t = 0; t < tokenCount; t++)
            {
                for (var h = 0; h < hiddenSize; h++)
                {
                    embedding[h] += hiddenState[batchIndex, t, h];
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
