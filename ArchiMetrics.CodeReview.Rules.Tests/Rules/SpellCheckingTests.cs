// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SpellCheckingTests.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the SpellCheckingTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Tests.Rules
{
	using System.Linq;
	using System.Threading.Tasks;
	using ArchiMetrics.Analysis;
	using ArchiMetrics.CodeReview.Rules.Code;
	using ArchiMetrics.CodeReview.Rules.Trivia;
	using ArchiMetrics.Common;
	using ArchiMetrics.Common.CodeReview;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using NUnit.Framework;

	public sealed class SpellCheckingTests
	{
		private SpellCheckingTests()
		{
		}

		public class GivenAMethodNameSpellingRule
		{
			private MethodNameSpellingRule _rule;

			[SetUp]
			public void Setup()
			{
				_rule = new MethodNameSpellingRule(new SpellChecker(new ExemptPatterns()));
			}

			[TestCase("SomMethod")]
			[TestCase("CalclateValue")]
			[TestCase("GetValu")]
			public void FindMispelledMethodNames(string methodName)
			{
				var method = CSharpSyntaxTree.ParseText(string.Format(@"public void {0}() {{ }}", methodName));
				var result = _rule.Evaluate(method.GetRoot()
					.ChildNodes()
					.OfType<MethodDeclarationSyntax>()
					.First());

				Assert.NotNull(result);
			}
		}

		public class GivenAMultiLineCommentLanguageRule
		{
			private MultiLineCommentLanguageRule _rule;

			[SetUp]
			public void Setup()
			{
				_rule = new MultiLineCommentLanguageRule(new SpellChecker(new ExemptPatterns()));
			}

			[TestCase("ASP.NET MVC is a .NET acronym.")]
			[TestCase("Donde esta la cerveza?")]
			[TestCase("Dette er ikke en engelsk kommentar.")]
			public void FindNonEnglishMultiLineComments(string comment)
			{
				var method = CSharpSyntaxTree.ParseText(
					string.Format(
@"public void SomeMethod() {{
/* {0} */
}}", 
   comment));
				var root = method.GetRoot().DescendantNodes().OfType<BlockSyntax>().First();
				var nodes = root
					.DescendantTrivia(descendIntoTrivia: true)
					.Where(t => t.IsKind(SyntaxKind.MultiLineCommentTrivia))
					.AsArray();
				var result = _rule.Evaluate(nodes.First());

				Assert.NotNull(result);
			}

			[TestCase(".NET has syntactic sugar the iterator pattern.")]
			[TestCase("This comment is in English.")]
			public void AcceptsEnglishMultiLineComments(string comment)
			{
				var method = CSharpSyntaxTree.ParseText(
					string.Format(
@"public void SomeMethod() {{
/* {0} */
}}", 
   comment));
				var root = method.GetRoot().DescendantNodes().OfType<BlockSyntax>().First();
				var nodes = root
					.DescendantTrivia(descendIntoTrivia: true)
					.Where(t => t.IsKind(SyntaxKind.MultiLineCommentTrivia))
					.AsArray();
				var result = _rule.Evaluate(nodes.First());

				Assert.Null(result);
			}

			[TestCase("<summary>Returns a string.</summary>")]
			[TestCase("<returns>A string.</returns>")]
			public void AcceptsEnglishMultiLineXmlComments(string comment)
			{
				var method = CSharpSyntaxTree.ParseText(
					string.Format(
						@"public void SomeMethod() {{
/* {0} */
}}", 
						comment));
				var root = method.GetRoot().DescendantNodes().OfType<BlockSyntax>().First();
				var nodes = root
					.DescendantTrivia(descendIntoTrivia: true)
					.Where(t => t.IsKind(SyntaxKind.MultiLineCommentTrivia))
					.AsArray();
				var result = _rule.Evaluate(nodes.First());

				Assert.Null(result);
			}
		}

		public class GivenASingleLineCommentLanguageRule
		{
			private SingleLineCommentLanguageRule _rule;

			[SetUp]
			public void Setup()
			{
				_rule = new SingleLineCommentLanguageRule(new SpellChecker(new ExemptPatterns()));
			}

			[TestCase("Dette er ikke en engelsk kommentar.")]
			[TestCase("<returns>Noget tekst.</returns>")]
			public void FindNonEnglishSingleLineComments(string comment)
			{
				var method = CSharpSyntaxTree.ParseText(
					string.Format(
@"public void SomeMethod() {{
//{0}
}}", 
   comment));
				var root = method.GetRoot().DescendantNodes().OfType<BlockSyntax>().First();
				var nodes = root
					.DescendantTrivia(descendIntoTrivia: true)
					.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
					.AsArray();
				var result = _rule.Evaluate(nodes.First());

				Assert.NotNull(result);
			}
		}

		public class GivenASolutionInspectorWithCommentLanguageRules
		{
			private NodeReviewer _reviewer;

			[SetUp]
			public void Setup()
			{
				var spellChecker = new SpellChecker(new ExemptPatterns());
				_reviewer = new NodeReviewer(new IEvaluation[] { new SingleLineCommentLanguageRule(spellChecker), new MultiLineCommentLanguageRule(spellChecker) }, Enumerable.Empty<ISymbolEvaluation>());
			}

			[TestCase("//Dette er ikke en engelsk kommentar.")]
			[TestCase("// <summary>Dette er ikke en engelsk kommentar.</summary>")]
			[TestCase("/* Dette er ikke en engelsk kommentar. */")]
			public async Task WhenInspectingCommentsThenDetectsSuspiciousLanguage(string comment)
			{
				var method = CSharpSyntaxTree.ParseText(
					string.Format(
@"public void SomeMethod() {{
{0}
}}", 
   comment));
				var root = method.GetRoot();

				var task = await _reviewer.Inspect(string.Empty, string.Empty, root, null, null);
				
				Assert.IsNotEmpty(task);
			}
		}
	}
}
