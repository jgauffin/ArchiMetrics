﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TooLowMaintainabilityIndexRuleTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2012
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the TooLowMaintainabilityIndexRuleTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Tests.Rules.Semantic
{
	using System.Linq;
	using ArchiMetrics.CodeReview.Semantic;
	using NUnit.Framework;
	using Roslyn.Compilers.CSharp;

	public sealed class TooLowMaintainabilityIndexRuleTests
	{
		private TooLowMaintainabilityIndexRuleTests()
		{
		}

		public class GivenATooLowMaintainabilityIndexRule : SolutionTestsBase
		{
			private const string HighMaintainability = @"public class MyClass
{
	public void DoSomething()
	{
		System.Console.WriteLine(""Hello World"");
	}
}";

			private TooLowMaintainabilityIndexRule _rule;
			
			[SetUp]
			public void Setup()
			{
				_rule = new TooLowMaintainabilityIndexRule();
			}

			[Test]
			public void WhenMethodHasHighMaintainabilityThenReturnsNull()
			{
				var solution = CreateSolution(HighMaintainability);
				var classDeclaration = (from p in solution.Projects
										from d in p.Documents
										let model = d.GetSemanticModel()
										let root = d.GetSyntaxRoot()
										from n in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
										select new
										{
											semanticModel = model, 
											node = n
										})
										.First();
				var result = _rule.Evaluate(classDeclaration.node, classDeclaration.semanticModel, solution);

				Assert.Null(result);
			}
		}
	}
}