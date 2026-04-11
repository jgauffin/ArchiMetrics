namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Numerics;

    internal static class VectorMath
    {
        internal static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
            {
                return 0.0;
            }

            double dot = 0, normA = 0, normB = 0;
            var i = 0;

            if (Vector.IsHardwareAccelerated && a.Length >= Vector<float>.Count)
            {
                var vDot = Vector<float>.Zero;
                var vNormA = Vector<float>.Zero;
                var vNormB = Vector<float>.Zero;

                var lastBlock = a.Length - (a.Length % Vector<float>.Count);
                for (; i < lastBlock; i += Vector<float>.Count)
                {
                    var va = new Vector<float>(a, i);
                    var vb = new Vector<float>(b, i);
                    vDot += va * vb;
                    vNormA += va * va;
                    vNormB += vb * vb;
                }

                for (var j = 0; j < Vector<float>.Count; j++)
                {
                    dot += vDot[j];
                    normA += vNormA[j];
                    normB += vNormB[j];
                }
            }

            for (; i < a.Length; i++)
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
