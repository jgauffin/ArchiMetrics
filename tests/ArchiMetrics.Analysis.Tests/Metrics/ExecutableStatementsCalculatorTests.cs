// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExecutableStatementsCalculatorTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ExecutableStatementsCalculatorTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Tests.Metrics
{
	using System.Linq;
	using ArchiMetrics.Analysis.Metrics;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Xunit;

    public sealed class ExecutableStatementsCalculatorTests
	{
		private ExecutableStatementsCalculatorTests()
		{
		}

		public class GivenAStatementsAnalyzer
		{
			private readonly ExecutableStatementsCalculator _analyzer;

			public GivenAStatementsAnalyzer()
			{
				_analyzer = new ExecutableStatementsCalculator();
			}

			[Fact]
			public void WhenOnlyAssigningConstThenHasZeroStatements()
			{
				const string Text = @"namespace Testing
			{
				public class TestClass {
					public void SomeMethod() {
						const string x = ""blah"";
					}
				}
			}";

				var syntaxTree = CSharpSyntaxTree.ParseText(Text);
				var root = syntaxTree
					.GetRoot()
					.DescendantNodes()
					.First(c => c.IsKind(SyntaxKind.MethodDeclaration));

				var statements = _analyzer.Calculate(root);

				Assert.Equal(0, statements);
			}

            [Theory]
			[InlineData(@"public void SomeMethod() { const string x = ""blah""; }", 0, SyntaxKind.MethodDeclaration)]
			[InlineData(@"public TestClass() { }", 1, SyntaxKind.ConstructorDeclaration)]
			[InlineData(@"public int GetValue() { return 1; }", 1, SyntaxKind.MethodDeclaration)]
			[InlineData(@"public double GetValue(double x)
		{
			if (x % 2 == 0.0)
			{
				return x;
			}
			return x + 1;
		}", 3, SyntaxKind.MethodDeclaration)]
			public void WhenCalculatingForMemberNodeHasExpectedStatementCount(string code, int expected, SyntaxKind kind)
			{
				var text = $@"namespace Testing
			{{
				public class TestClass {{
					{code}
				}}
			}}";

				var syntaxTree = CSharpSyntaxTree.ParseText(text);
				var root = syntaxTree
					.GetRoot()
					.DescendantNodes()
					.First(c => c.IsKind(kind));
				var statements = _analyzer.Calculate(root);

				Assert.Equal(expected, statements);
			}

            [Theory]
			[InlineData(@"public void SomeMethod() {
						const string x = ""blah"";
					}", 0)]
			[InlineData(@"public TestClass() { }", 1)]
			[InlineData(@"public int Value { get; set; }", 2)]
			[InlineData(@"public int Value { get; }", 1)]
			[InlineData(@"public int Value { set; }", 1)]
			[InlineData(@"public int GetValue() { return 1; }", 1)]
			[InlineData(@"public double GetValue(double x)
		{
			if (x % 2 == 0.0)
			{
				return x;
			}
			return x + 1;
		}", 3)]
			public void WhenCountingStatementsThenHasExpectedCount(string code, int count)
			{
				var text = $@"namespace Testing
			{{
				public class TestClass {{
					{code}
				}}
			}}";

				var syntaxTree = CSharpSyntaxTree.ParseText(text);
				var root = syntaxTree.GetRoot();
				var statements = _analyzer.Calculate(root);

				Assert.Equal(count, statements);
			}
		}

		/// <summary>
		/// Building a collection is one piece of work however many entries it holds. Charging a statement
		/// per element made lookup tables — code with no branches and nothing to follow — measure as the
		/// largest members in a solution, dragging their maintainability index down with them.
		/// </summary>
		public class GivenAnInitializer
		{
			private readonly ExecutableStatementsCalculator _analyzer;

			public GivenAnInitializer()
			{
				_analyzer = new ExecutableStatementsCalculator();
			}

			private int CalculateForMethod(string code)
			{
				var text = $@"namespace Testing
			{{
				public class TestClass {{
					{code}
				}}
			}}";

				var root = CSharpSyntaxTree.ParseText(text)
					.GetRoot()
					.DescendantNodes()
					.First(c => c.IsKind(SyntaxKind.MethodDeclaration));

				return _analyzer.Calculate(root);
			}

			[Fact]
			public void WhenArrayInitializerHasManyElementsThenItCountsAsOneStatement()
			{
				var count = CalculateForMethod("public int[] GetValues() { return new[] { 1, 2, 3, 4, 5, 6 }; }");

				Assert.Equal(2, count);
			}

			[Fact]
			public void WhenInitializerGrowsThenStatementCountDoesNotGrow()
			{
				var small = CalculateForMethod("public int[] GetValues() { return new[] { 1 }; }");
				var large = CalculateForMethod("public int[] GetValues() { return new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; }");

				Assert.Equal(small, large);
			}

			[Fact]
			public void WhenObjectInitializerIsUsedThenItCountsAsOneStatement()
			{
				var count = CalculateForMethod(@"public object Build() { return new Widget { Width = 1, Height = 2, Depth = 3 }; }");

				Assert.Equal(2, count);
			}
		}
	}
}
