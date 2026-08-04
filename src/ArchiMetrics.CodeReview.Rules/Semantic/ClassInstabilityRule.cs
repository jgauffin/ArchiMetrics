// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClassInstabilityRule.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ClassInstabilityRule type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Semantic
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Analysis;
	using Analysis.Common;
	using Analysis.Common.CodeReview;
	using Analysis.ReferenceResolvers;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	internal class ClassInstabilityRule : SemanticEvaluationBase, ICweMapping
	{
		public IReadOnlyList<string> CweIds { get; } = new[] { "CWE-1047" };
		public Iso5055Category Iso5055Category => Iso5055Category.Maintainability;

		public override string ID
		{
			get
			{
				return "AM0053";
			}
		}

		public override ImpactLevel ImpactLevel
		{
			get
			{
				return ImpactLevel.Type;
			}
		}

		public override SyntaxKind EvaluatedKind
		{
			get
			{
				return SyntaxKind.ClassDeclaration;
			}
		}

		public override string Title
		{
			get
			{
				return "Unstable Class";
			}
		}

		public override string Suggestion
		{
			get
			{
				return "Refactor class dependencies.";
			}
		}

		public override CodeQuality Quality
		{
			get
			{
				return CodeQuality.NeedsRefactoring;
			}
		}

		public override QualityAttribute QualityAttribute
		{
			get
			{
				return QualityAttribute.Maintainability | QualityAttribute.Modifiability;
			}
		}

		protected override async Task<EvaluationResult> EvaluateImpl(SyntaxNode node, SemanticModel semanticModel, Solution solution)
		{
			var symbol = (ITypeSymbol)semanticModel.GetDeclaredSymbol(node);
			var efferent = GetReferencedTypes(node, symbol, semanticModel).AsArray();

			// Afferent coupling is read from the solution-wide reference index, which is built once
			// per solution and shared by every rule, so this is a dictionary lookup rather than a
			// fresh scan of the whole solution for each class.
			//
			// This also corrects the number. The previous implementation asked
			// SymbolFinder.FindCallersAsync for the callers of a *type*, but that API only reports
			// callers of callable symbols such as methods and properties. A class symbol therefore
			// always came back with zero callers, so stability was efferent / (efferent + 0) == 1
			// and every class that referenced anything at all was reported as unstable, no matter
			// how many other types depended on it.
			var references = await solution.FindReferences(symbol).ConfigureAwait(false);
			var afferent = references.Locations
				.Where(x => x.ReferencingType != null)
				.Where(x => x.ReferencingType.ToDisplayString() != symbol.ToDisplayString())
				.Where(x => !IsReferencedFromTest(x))
				.Select(x => x.ReferencingType)
				.DistinctBy(s => s.ToDisplayString())
				.AsArray();

			var efferentLength = (double)efferent.Length;
			var stability = efferentLength / (efferentLength + afferent.Length);
			if (stability >= 0.8)
			{
				return new EvaluationResult
				{
					ImpactLevel = ImpactLevel.Project,
					Quality = CodeQuality.NeedsReview,
					QualityAttribute = QualityAttribute.CodeQuality | QualityAttribute.Conformance,
					Snippet = node.ToFullString()
				};
			}

			return null;
		}

		/// <summary>
		/// Test code depends on production code by design, so counting it would make a class look
		/// more depended-upon - and therefore more stable - than it really is in the shipping
		/// application. Only references from non-test code describe the real coupling.
		/// </summary>
		private static bool IsReferencedFromTest(ReferenceLocation reference)
		{
			var sourceTree = reference.Location.SourceTree;
			if (sourceTree == null)
			{
				return false;
			}

			var method = sourceTree.GetRoot().FindToken(reference.Location.SourceSpan.Start).GetMethod();

			return method != null
				   && method.AttributeLists.Any(a => a.Attributes.Any(b => b.Name.ToString().IsKnownTestAttribute()));
		}

		private static IEnumerable<ITypeSymbol> GetReferencedTypes(SyntaxNode classDeclaration, ISymbol sourceSymbol, SemanticModel semanticModel)
		{
			var typeSyntaxes = classDeclaration.DescendantNodesAndSelf().OfType<TypeSyntax>();
			var commonSymbolInfos = typeSyntaxes.Select(x => semanticModel.GetSymbolInfo(x)).AsArray();
			var members = commonSymbolInfos
				.Select(x => x.Symbol)
				.Where(x => x != null)
				.Select(x =>
				{
					var typeSymbol = x as ITypeSymbol;
					return typeSymbol == null ? x.ContainingType : x;
				})
				.Cast<ITypeSymbol>()
				.WhereNotNull()
				.DistinctBy(x => x.ToDisplayString())
				.Where(x => x != sourceSymbol)
				.AsArray();

			return members;
		}
	}
}