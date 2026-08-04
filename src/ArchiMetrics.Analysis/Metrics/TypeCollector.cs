// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TypeCollector.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the TypeCollector type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Metrics
{
	using System.Collections.Generic;
	using Common;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	internal sealed class TypeCollector
	{
		public IEnumerable<TypeDeclarationSyntax> GetTypes(SyntaxNode namespaceNode)
		{
			var innerCollector = new InnerTypeCollector();
			return innerCollector.GetTypes(namespaceNode);
		}

		private class InnerTypeCollector : CSharpSyntaxWalker
		{
			private readonly IList<TypeDeclarationSyntax> _types;

			public InnerTypeCollector()
				: base(SyntaxWalkerDepth.Node)
			{
				_types = new List<TypeDeclarationSyntax>();
			}

			public IEnumerable<TypeDeclarationSyntax> GetTypes(SyntaxNode namespaceNode)
			{
				var node = namespaceNode as BaseNamespaceDeclarationSyntax;
				if (node != null)
				{
					Visit(node);
				}

				return _types.AsArray();
			}

			public override void VisitClassDeclaration(ClassDeclarationSyntax node)
			{
				base.VisitClassDeclaration(node);
				_types.Add(node);
			}

			public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
			{
				base.VisitInterfaceDeclaration(node);
				_types.Add(node);
			}

			public override void VisitStructDeclaration(StructDeclarationSyntax node)
			{
				base.VisitStructDeclaration(node);
				_types.Add(node);
			}

			/// <summary>
			/// Collects <c>record</c> and <c>record struct</c> declarations.
			/// </summary>
			/// <remarks>
			/// Records are ordinary types that carry logic and therefore belong in the metrics
			/// just like classes and structs. Skipping them would leave holes in the report and
			/// make any average computed over the types unrepresentative of the code base.
			/// </remarks>
			public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
			{
				base.VisitRecordDeclaration(node);
				_types.Add(node);
			}
		}
	}
}
