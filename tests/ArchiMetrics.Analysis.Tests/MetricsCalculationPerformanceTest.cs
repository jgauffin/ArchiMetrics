// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MetricsCalculationPerformanceTest.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the MetricsCalculationPerformanceTest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.Metrics;
    using Common;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.MSBuild;
    using Xunit;

    /// <summary>
    /// Guards the cost of calculating metrics over a real solution. See
    /// <c>RuleEvaluationPerformanceTest</c> for why these live behind the Performance category.
    /// </summary>
    [Trait("Category", "Performance")]
    public class MetricsCalculationPerformanceTest
    {
        private const double MaxAverageSeconds = 30.0;
        private const int Iterations = 3;

        private readonly ProjectMetricsCalculator _calculator;

        public MetricsCalculationPerformanceTest()
        {
            _calculator = new ProjectMetricsCalculator(new CodeMetricsCalculator(new TypeDocumentationFactory(), new MemberDocumentationFactory()));
        }

        [Fact]
        public async Task MeasureSolutionAnalysisPerformance()
        {
            using (var workspace = MSBuildWorkspace.Create())
            {
                var path = @"..\..\..\..\..\archimetrics.sln".GetLowerCaseFullPath();
                var solution = await workspace.OpenSolutionAsync(path);
                var durations = new List<double>();
                for (var i = 0; i < Iterations; i++)
                {
                    var sw = Stopwatch.StartNew();
                    await PerformReview(solution);
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalSeconds);
                }

                Assert.True(
                    durations.Average() < MaxAverageSeconds,
                    $"Average solution analysis took {durations.Average():F1}s, expected under {MaxAverageSeconds}s. Runs: {string.Join(", ", durations.Select(x => $"{x:F1}s"))}");
            }
        }

        [Fact]
        public async Task MeasureProjectAnalysisPerformance()
        {
            using (var workspace = MSBuildWorkspace.Create())
            {
                var path = @"..\..\..\..\..\src\ArchiMetrics.Analysis\ArchiMetrics.Analysis.csproj".GetLowerCaseFullPath();
                var project = await workspace.OpenProjectAsync(path);
                var durations = new List<double>();
                for (var i = 0; i < Iterations; i++)
                {
                    var sw = Stopwatch.StartNew();
                    await PerformReview(project);
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalSeconds);
                }

                Assert.True(
                    durations.Average() < MaxAverageSeconds,
                    $"Average project analysis took {durations.Average():F1}s, expected under {MaxAverageSeconds}s. Runs: {string.Join(", ", durations.Select(x => $"{x:F1}s"))}");
            }
        }

        private async Task<int> PerformReview(Solution solution)
        {
            var results = await _calculator.Calculate(solution);
            var amount = results.AsArray();
            return amount.Length;
        }

        private async Task<IProjectMetric> PerformReview(Project project)
        {
            var results = await _calculator.Calculate(project, null);

            return results;
        }
    }
}
