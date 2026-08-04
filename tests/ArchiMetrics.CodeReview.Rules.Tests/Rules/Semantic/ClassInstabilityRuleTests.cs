// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClassInstabilityRuleTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ClassInstabilityRuleTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Tests.Rules.Semantic
{
    using System.Linq;
    using System.Threading.Tasks;
    using ArchiMetrics.CodeReview.Rules.Semantic;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Xunit;

    /// <summary>
    /// These tests pin down what the rule currently reports. The rule is about to have its
    /// caller lookup rewritten for performance, and a performance change must not quietly become
    /// a behaviour change - so the observable verdict for a stable and an unstable class is
    /// captured here first, and must read the same afterwards.
    /// </summary>
    public sealed class ClassInstabilityRuleTests
    {
        private ClassInstabilityRuleTests()
        {
        }

        public class GivenAClassInstabilityRule : SolutionTestsBase
        {
            // Depends on three other types and nothing in the solution depends on it, so all of
            // its coupling points outwards - the definition of an unstable class.
            private const string Unstable = @"namespace MyNamespace
{
	public class Dependency1 { }

	public class Dependency2 { }

	public class Dependency3 { }

	public class Unstable
	{
		public Dependency1 First() { return new Dependency1(); }

		public Dependency2 Second() { return new Dependency2(); }

		public Dependency3 Third() { return new Dependency3(); }
	}
}";

            // Three types depend on it and it depends on nothing, so its coupling points inwards.
            private const string Stable = @"namespace MyNamespace
{
	public class Stable
	{
		public int Value() { return 1; }
	}

	public class Consumer1
	{
		public int Use() { return new Stable().Value(); }
	}

	public class Consumer2
	{
		public int Use() { return new Stable().Value(); }
	}

	public class Consumer3
	{
		public int Use() { return new Stable().Value(); }
	}
}";

            private readonly ClassInstabilityRule _rule;

            public GivenAClassInstabilityRule()
            {
                _rule = new ClassInstabilityRule();
            }

            [Fact]
            public async Task WhenAnalyzingAnUnstableClassThenReturnsError()
            {
                var result = await Evaluate(Unstable, "Unstable");

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WhenAnalyzingAStableClassThenReturnsNoError()
            {
                var result = await Evaluate(Stable, "Stable");

                Assert.Null(result);
            }

            private async Task<EvaluationResultHolder> EvaluateCore(string code, string className)
            {
                var solution = CreateSolution(code);
                var found = (from p in solution.Projects
                             from d in p.Documents
                             let model = d.GetSemanticModelAsync().Result
                             let root = d.GetSyntaxRootAsync().Result
                             from n in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                             where n.Identifier.ValueText == className
                             select new EvaluationResultHolder
                             {
                                 Model = model,
                                 Node = n,
                                 Solution = solution
                             }).First();

                await Task.Yield();
                return found;
            }

            private async Task<Analysis.Common.CodeReview.EvaluationResult> Evaluate(string code, string className)
            {
                var context = await EvaluateCore(code, className);

                return await _rule.Evaluate(context.Node, context.Model, context.Solution);
            }

            private class EvaluationResultHolder
            {
                public SemanticModel Model { get; set; }

                public ClassDeclarationSyntax Node { get; set; }

                public Solution Solution { get; set; }
            }
        }
    }
}
