// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PublicInterfaceImplementationWarningRule.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2012
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the PublicInterfaceImplementationWarningRule type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Code
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ArchiMetrics.Common.CodeReview;
	using Roslyn.Compilers.CSharp;

	internal class PublicInterfaceImplementationWarningRule : CodeEvaluationBase
	{
		private static IEnumerable<Type> _appDomainTypes;

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
				return "Public Interface Implementation";
			}
		}

		public override string Suggestion
		{
			get
			{
				return "Consider whether the interface implementation also needs to be public.";
			}
		}

		public override CodeQuality Quality
		{
			get
			{
				return CodeQuality.NeedsReview;
			}
		}

		public override QualityAttribute QualityAttribute
		{
			get
			{
				return QualityAttribute.Modifiability;
			}
		}

		public override ImpactLevel ImpactLevel
		{
			get
			{
				return ImpactLevel.Project;
			}
		}

		protected override EvaluationResult EvaluateImpl(SyntaxNode node)
		{
			var classDeclaration = (ClassDeclarationSyntax)node;
			if (classDeclaration.BaseList != null && (classDeclaration.BaseList.Types.Any(SyntaxKind.IdentifierName) || classDeclaration.BaseList.Types.Any(SyntaxKind.GenericName)))
			{
				var s = classDeclaration.BaseList.Types.First(x => x.Kind == SyntaxKind.IdentifierName || x.Kind == SyntaxKind.GenericName);
				if (((SimpleNameSyntax)s).Identifier.ValueText.StartsWith("I")
					&& classDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
				{
					var interfaceName = ((SimpleNameSyntax)s).Identifier.ValueText;
					if (!IsKnownInterface(interfaceName))
					{
						var snippet = classDeclaration.ToFullString();

						return new EvaluationResult
								   {
									   Snippet = snippet
								   };
					}
				}
			}

			return null;
		}

		private bool IsKnownInterface(string interfaceName)
		{
			try
			{
				var types = _appDomainTypes ?? (_appDomainTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()));
				return types
								.Any(
									t =>
									string.Equals(t.Name, interfaceName, StringComparison.InvariantCultureIgnoreCase)
									|| string.Equals(t.FullName, interfaceName));
			}
			catch
			{
				return false;
			}
		}
	}
}