namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEmbeddingProvider
    {
        Task<IReadOnlyList<float[]>> GetEmbeddings(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
    }
}
