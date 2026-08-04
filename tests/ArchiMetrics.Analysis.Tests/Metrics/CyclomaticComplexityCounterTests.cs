// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CyclomaticComplexityCounterTests.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the CyclomaticComplexityCounterTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.Linq;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Xunit;

    /// <summary>
    /// Pins the counter to the standard McCabe definition: complexity starts at 1 and gains 1 at each point
    /// where control flow can take a different path.
    /// </summary>
    /// <remarks>
    /// The metric is only useful if two methods of equal real complexity score equally. Counting a construct
    /// that never branches inflates one coding style over another, and a tool whose purpose is ranking code
    /// then ranks it by fashion rather than by cost. That is why the cases below assert exact numbers.
    /// </remarks>
    public sealed class CyclomaticComplexityCounterTests
    {
        private CyclomaticComplexityCounterTests()
        {
        }

        /// <summary>
        /// Parses a snippet and measures its first method declaration. A semantic model is built even though
        /// the counter is purely syntactic, so that the tests exercise the same call shape as production.
        /// </summary>
        private static int CalculateForMethod(string method)
        {
            var hasNamespace = method.Contains("namespace ");
            var code = hasNamespace
                ? method
                : $"class __Wrapper__ {{ {method} }}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "x",
                syntaxTrees: new[] { tree },
                references:
                new MetadataReference[]
                {
                    MetadataReference.CreateFromFile(typeof (object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof (Task).Assembly.Location)
                });

            var model = compilation.GetSemanticModel(tree, true);
            var syntaxNode = tree
                .GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .First();

            return new CyclomaticComplexityCounter().Calculate(syntaxNode, model);
        }

        public class GivenACyclomaticComplexityAnalyzer
        {
            [Theory]
            [InlineData("public abstract void DoSomething();", 1)]
            [InlineData("void DoSomething();", 1)]
            [InlineData("void DoSomething(){ var x = a && b; }", 2)]
            [InlineData(@"public void DoSomething(){
	try
	{
		var x = 1 + 2;
		var y = x + 2;
	}
	catch
	{
		throw new Exception();
	}
}", 2)]
            [InlineData(@"public void DoSomething(){
	try
	{
		var x = 1 + 2;
		var y = x + 2;
	}
	catch(ArgumentNullException ane)
	{
		throw new Exception();
	}
	catch(OutOfRangeException ane)
	{
		throw new Exception();
	}
}", 3)]
            [InlineData(@"public void DoSomething(){
	if(x == 1)
	{
		var y = x + 2;
	}
	else
	{
		var y = 1 + 2;
	}
}", 2)]
            [InlineData(@"public int DoSomething(){
	switch(x){
		case ""a"": return 1;
		case ""b"": return 2;
		default: return 0;
	}
}", 3)]
            [InlineData(@"public int DoSomething(){
	var x = a > 2 ? 1 : 0;
	}
}", 2)]
            [InlineData(@"public int DoSomething(){
	var x = a ?? new object();
	}
}", 2)]
            [InlineData(@"public int DoSomething(){
		var numbers = new[] { 1, 2, 3 };
		var n = numbers.Where(n => n != 1).AsArray();
	}
}", 1)]
            [InlineData(@"
namespace MyNs
{
	using System;
	using System.Threading.Tasks;

	public class MyClass
	{
		public void DoSomething()
		{
				var task = Task.Factory.StartNew(() => { Console.WriteLine(""blah""); });
		}
	}
}", 1)]
            public void MethodHasExpectedComplexity(string method, int expectedComplexity)
            {
                Assert.Equal(expectedComplexity, CalculateForMethod(method));
            }

            [Theory]
            [InlineData(@"namespace MyNs
{
	public class MyClass
	{
		private EventHandler _innerHandler;

		public event EventHandler MyEvent
		{
			add { _innerHandler += value; }
			remove { _innerHandler -= value; }
		}
	}
}", 1)]
            public void EventAddAccessorHasExpectedComplexity(string code, int expectedComplexity)
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var compilation = CSharpCompilation.Create(
                    "x",
                    syntaxTrees: new[] { tree },
                    references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(Task).Assembly.Location) },
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: new[] { "System", "System.Threading.Tasks" }));

                var model = compilation.GetSemanticModel(tree, true);
                var syntaxNode = tree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<AccessorDeclarationSyntax>()
                    .First();

                var result = new CyclomaticComplexityCounter().Calculate(syntaxNode, model);

                Assert.Equal(expectedComplexity, result);
            }
        }

        public class GivenBranchingConstructs
        {
            [Theory]
            [InlineData("public void Go(int x) { do { } while (x > 0); }")]
            [InlineData("public void Go(int[] x) { foreach (var i in x) { } }")]
            [InlineData("public void Go(int x) { for (var i = 0; i < x; i++) { } }")]
            [InlineData("public void Go(int x) { while (x > 0) { } }")]
            public void WhenMethodHasOneLoopThenComplexityIsTwo(string code)
            {
                Assert.Equal(2, CalculateForMethod(code));
            }

            /// <summary>
            /// A filtered catch decides twice: whether the exception type matches, and whether the filter
            /// expression holds. Both are real paths through the method.
            /// </summary>
            [Fact]
            public void WhenCatchHasFilterThenFilterCounts()
            {
                var code = @"public void Go(bool flag)
{
    try { } catch (System.Exception) when (flag) { }
}";

                Assert.Equal(3, CalculateForMethod(code));
            }
        }

        public class GivenConstructsThatDoNotBranch
        {
            /// <summary>
            /// Negation evaluates a boolean, it does not choose a path. Counting it taxed guard clauses,
            /// which are usually the more readable way to write the same logic.
            /// </summary>
            [Fact]
            public void WhenMethodNegatesABooleanThenNegationDoesNotCount()
            {
                Assert.Equal(2, CalculateForMethod("public void Go(bool a) { if (!a) { } }"));
            }

            [Fact]
            public void WhenMethodUsesDefaultExpressionThenItDoesNotCount()
            {
                Assert.Equal(1, CalculateForMethod("public int Go() { return default(int); }"));
            }

            /// <summary>
            /// The decision belongs to the enclosing <c>if</c>, which is already counted. Counting the jump
            /// as well charged the same branch twice.
            /// </summary>
            [Fact]
            public void WhenLoopUsesContinueThenOnlyTheGuardingIfCounts()
            {
                var code = @"public void Go(int[] items)
{
    foreach (var i in items)
    {
        if (i == 0) { continue; }
    }
}";

                Assert.Equal(3, CalculateForMethod(code));
            }

            [Fact]
            public void WhenMethodUsesGotoThenOnlyTheGuardingIfCounts()
            {
                var code = @"public void Go(int x)
{
    start:
    x++;
    if (x < 10) { goto start; }
}";

                Assert.Equal(2, CalculateForMethod(code));
            }
        }

        public class GivenModernCSharp
        {
            /// <summary>
            /// Switch expressions are branches just as switch statements are. Missing them let a method be
            /// rewritten into modern C# and appear to become simpler without any real change.
            /// </summary>
            [Fact]
            public void WhenSwitchExpressionHasArmsThenEachNonDiscardArmCounts()
            {
                var code = @"public string Go(int x)
{
    return x switch { 1 => ""a"", 2 => ""b"", _ => ""c"" };
}";

                Assert.Equal(3, CalculateForMethod(code));
            }

            [Fact]
            public void WhenPatternUsesAndCombinatorThenItCounts()
            {
                Assert.Equal(3, CalculateForMethod("public void Go(object o) { if (o is int and not 0) { } }"));
            }

            [Fact]
            public void WhenPatternUsesOrCombinatorThenItCounts()
            {
                Assert.Equal(3, CalculateForMethod("public void Go(object o) { if (o is 1 or 2) { } }"));
            }

            [Fact]
            public void WhenMethodUsesCoalesceAssignmentThenItCounts()
            {
                Assert.Equal(2, CalculateForMethod("public void Go(string a) { a ??= string.Empty; }"));
            }

            [Fact]
            public void WhenMethodUsesNullConditionalAccessThenItCounts()
            {
                Assert.Equal(2, CalculateForMethod("public int? Go(string a) { return a?.Length; }"));
            }
        }

        public class GivenLambdaArguments
        {
            /// <summary>
            /// A lambda passed as an argument had its body walked twice, so every branch inside it counted
            /// double. Methods that pass predicates were penalised purely for using LINQ.
            /// </summary>
            [Fact]
            public void WhenLambdaArgumentContainsABranchThenItIsCountedOnce()
            {
                var code = @"public int DoSomething(){
	var numbers = new[] { 1, 2, 3 };
	var odds = numbers.Where(n => { if(n != 1) { return n %2 == 0; } else { return false; } }).AsArray();
}";

                Assert.Equal(2, CalculateForMethod(code));
            }

            [Fact]
            public void WhenLambdaArgumentIsAnExpressionThenItsBranchIsCountedOnce()
            {
                var code = @"public void Go(System.Collections.Generic.List<int> items)
{
    items.ForEach(i => System.Console.WriteLine(i > 0 ? ""y"" : ""n""));
}";

                Assert.Equal(2, CalculateForMethod(code));
            }
        }
    }
}
