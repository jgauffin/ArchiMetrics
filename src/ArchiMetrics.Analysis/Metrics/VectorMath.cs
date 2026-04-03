namespace ArchiMetrics.Analysis.Metrics
{
    using System;

    internal static class VectorMath
    {
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
