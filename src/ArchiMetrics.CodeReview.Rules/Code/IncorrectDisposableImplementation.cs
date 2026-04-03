// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IncorrectDisposableImplementation.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the IncorrectDisposableImplementation type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Code
{
	using System.Collections.Generic;
	using System.Linq;
	using Analysis.Common;
	using Analysis.Common.CodeReview;
	using ArchiMetrics.Analysis;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	internal class IncorrectDisposableImplementation : CodeEvaluationBase, ICweMapping
	{
		public IReadOnlyList<string> CweIds { get; } = new[] { "CWE-401" };
		public Iso5055Category Iso5055Category => Iso5055Category.Reliability;

		public override string ID
		{
			get
			{
				return "AM0018";
			}
		}

		public override SyntaxKind EvaluatedKind
		{
			get { return SyntaxKind.ClassDeclaration; }
		}

		public override string Title
		{
			get { return "Incorrect Dispose pattern implementation"; }
		}

		public override string Suggestion
		{
			get { return "Implement dispose pattern with finalizer and separate disposal of managed and unmanaged resources."; }
		}

		public override CodeQuality Quality
		{
			get { return CodeQuality.NeedsReview; }
		}

		public override QualityAttribute QualityAttribute
		{
			get { return QualityAttribute.CodeQuality | QualityAttribute.Conformance; }
		}

		public override ImpactLevel ImpactLevel
		{
			get { return ImpactLevel.Type; }
		}

		protected override EvaluationResult EvaluateImpl(SyntaxNode node)
		{
			var classDeclaration = (ClassDeclarationSyntax)node;
			if (classDeclaration.BaseList == null
				|| !classDeclaration.BaseList.Types.Any(t => t.Type is IdentifierNameSyntax id && id.Identifier.ValueText.Contains("IDisposable")))
			{
				return null;
			}

			var isSealed = classDeclaration.Modifiers.Any(SyntaxKind.SealedKeyword);

			var disposeMethods = classDeclaration.ChildNodes().OfType<MethodDeclarationSyntax>()
				.Where(m => m.Identifier.ValueText == "Dispose")
				.Where(m =>
					{
						var predefinedType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword));
						return m.ParameterList.Parameters.Count == 0
									   || (m.ParameterList.Parameters.Count == 1 && m.ParameterList.Parameters[0].Type.EquivalentTo(predefinedType));
					}).AsArray();

			if (isSealed)
			{
				// Sealed classes don't need the full virtual Dispose pattern.
				// A parameterless Dispose() that calls GC.SuppressFinalize(this) is sufficient.
				var parameterlessDispose = disposeMethods.FirstOrDefault(m => m.ParameterList.Parameters.Count == 0);
				if (parameterlessDispose == null || !InvokesSuppressFinalize(parameterlessDispose))
				{
					return new EvaluationResult
					{
						Snippet = node.ToFullString()
					};
				}
			}
			else
			{
				// Unsealed classes need the full pattern: Dispose(), Dispose(bool), and a finalizer.
				var destructor = classDeclaration
					.ChildNodes()
					.OfType<DestructorDeclarationSyntax>()
					.FirstOrDefault(d => d.Body != null && d.Body.ChildNodes().Any(InvokesDispose));
				if (disposeMethods.Length < 2 || destructor == null)
				{
					return new EvaluationResult
					{
						Snippet = node.ToFullString()
					};
				}
			}

			return null;
		}

		private static bool InvokesSuppressFinalize(MethodDeclarationSyntax method)
		{
			// Look for GC.SuppressFinalize(...) anywhere in the method body.
			return method.Body != null && method.Body.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.Any(inv =>
					inv.Expression is MemberAccessExpressionSyntax memberAccess
					&& memberAccess.Name.Identifier.ValueText == "SuppressFinalize"
					&& memberAccess.Expression is IdentifierNameSyntax identifierName
					&& identifierName.Identifier.ValueText == "GC");
		}

		private bool InvokesDispose(SyntaxNode node)
		{
			var expression = node as ExpressionStatementSyntax;
			if (expression != null)
			{
				var invocation = expression.Expression as InvocationExpressionSyntax;
				if (invocation != null)
				{
					var identifier = invocation.Expression as IdentifierNameSyntax;
					if (identifier != null
						&& identifier.Identifier.ValueText == "Dispose"
						&& invocation.ArgumentList != null
						&& invocation.ArgumentList.Arguments.Count == 1
						&& invocation.ArgumentList.Arguments[0].EquivalentTo(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression, SyntaxFactory.Token(SyntaxKind.FalseKeyword)))))
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}