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

    public class MetricsCalculationPerformanceTest
    {
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
                for (var i = 0; i < 5; i++)
                {
                    var sw = Stopwatch.StartNew();
                    PerformReview(solution).Wait();
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalSeconds);
                }

                Assert.True(durations.Average() < 90.0);
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
                for (var i = 0; i < 5; i++)
                {
                    var sw = Stopwatch.StartNew();
                    PerformReview(project).Wait();
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalSeconds);
                }

                Assert.True(durations.Average() < 90.0);
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