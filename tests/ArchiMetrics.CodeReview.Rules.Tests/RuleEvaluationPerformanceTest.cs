// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RuleEvaluationPerformanceTest.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the RuleEvaluationPerformanceTest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Analysis;
    using Analysis.Common;
    using Analysis.Common.CodeReview;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.MSBuild;
    using Moq;
    using Xunit;

    /// <summary>
    /// Guards the cost of reviewing a whole solution. Reviewing this repository is a realistic
    /// workload, and it should complete in seconds - if it starts taking minutes again, something
    /// has gone back to re-deriving reference information per symbol instead of reading the shared
    /// index, and this test is the alarm.
    /// <para>
    /// Excluded from the default test run via the Performance category, because opening and
    /// analysing a real solution is far too slow to sit in the normal edit/test loop.
    /// </para>
    /// </summary>
    [Trait("Category", "Performance")]
    public class RuleEvaluationPerformanceTest
    {
        private const double MaxAverageSeconds = 30.0;
        private const int Iterations = 3;

        private readonly NodeReviewer _reviewer;

        public RuleEvaluationPerformanceTest()
        {
            var spellChecker = new Mock<ISpellChecker>();
            spellChecker.Setup(x => x.Spell(It.IsAny<string>())).Returns(true);

            _reviewer = new NodeReviewer(AllRules.GetSyntaxRules(spellChecker.Object).AsArray(), AllRules.GetSymbolRules());
        }

        [Fact]
        public async Task MeasurePerformance()
        {
            using (var workspace = MSBuildWorkspace.Create())
            {
                // Opened once, outside the loop. Re-opening the solution on every iteration meant
                // the measurement was dominated by MSBuild load time rather than by the rules the
                // test claims to be measuring.
                var path = @"..\..\..\..\..\archimetrics.sln".GetLowerCaseFullPath();
                var solution = await workspace.OpenSolutionAsync(path);

                var durations = new List<double>();
                var findings = 0;
                for (var i = 0; i < Iterations; i++)
                {
                    var sw = Stopwatch.StartNew();
                    findings = await PerformReview(solution);
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalSeconds);
                }

                // Speed is only meaningful if the review still sees the code. Without this, a
                // change that quietly stopped resolving references would look like a large
                // performance win instead of the regression it is.
                Assert.True(findings > 0, "The review returned no findings at all, so the timing below is measuring nothing.");

                Assert.True(
                    durations.Average() < MaxAverageSeconds,
                    $"Average review took {durations.Average():F1}s, expected under {MaxAverageSeconds}s. Runs: {string.Join(", ", durations.Select(x => $"{x:F1}s"))}");
            }
        }

        private async Task<int> PerformReview(Solution solution)
        {
            var results = await _reviewer.Inspect(solution);
            var amount = results.AsArray();
            return amount.Length;
        }
    }
}
