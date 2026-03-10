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
    using Microsoft.CodeAnalysis.MSBuild;
    using Moq;
    using Xunit;

    public class RuleEvaluationPerformanceTest
    {
        private readonly NodeReviewer _reviewer;

        public RuleEvaluationPerformanceTest()
        {
            var spellChecker = new Mock<ISpellChecker>();
            spellChecker.Setup(x => x.Spell(It.IsAny<string>())).Returns(true);

            _reviewer = new NodeReviewer(AllRules.GetSyntaxRules(spellChecker.Object).AsArray(), AllRules.GetSymbolRules());
        }

        [Fact]
        public void MeasurePerformance()
        {
            var durations = new List<double>();
            for (var i = 0; i < 10; i++)
            {
                var sw = Stopwatch.StartNew();
                PerformReview().Wait();
                sw.Stop();
                durations.Add(sw.Elapsed.TotalSeconds);
            }

            Assert.True(durations.Average() < 90.0);
        }

        private async Task<int> PerformReview()
        {
            using (var workspace = MSBuildWorkspace.Create())
            {
                var path = @"..\..\..\..\..\archimetrics.sln".GetLowerCaseFullPath();
                var solution = await workspace.OpenSolutionAsync(path);
                var results = await _reviewer.Inspect(solution);
                var amount = results.AsArray();
                return amount.Length;
            }
        }
    }
}