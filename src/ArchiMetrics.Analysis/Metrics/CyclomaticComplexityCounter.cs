// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CyclomaticComplexityCounter.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the CyclomaticComplexityCounter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Metrics
{
	using System.Collections.Generic;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	internal sealed class CyclomaticComplexityCounter
	{
		public int Calculate(SyntaxNode node, SemanticModel semanticModel)
		{
			var analyzer = new InnerComplexityAnalyzer(semanticModel);
			var result = analyzer.Calculate(node);

			return result;
		}

		private class InnerComplexityAnalyzer : CSharpSyntaxWalker
		{
			/// <summary>
			/// Expression-level constructs that introduce a decision. Each one lets execution reach the
			/// following code by more than one route, which is the definition of a decision point.
			/// </summary>
			/// <remarks>
			/// Deliberately absent: <c>!</c> and <c>not</c>. Negation evaluates a boolean, it does not choose
			/// a path, so counting it would tax guard clauses — usually the more readable way to express the
			/// same logic — without measuring any extra branch.
			/// </remarks>
			private static readonly HashSet<SyntaxKind> Contributors = new HashSet<SyntaxKind>
																{
																	SyntaxKind.CaseSwitchLabel,
																	SyntaxKind.CasePatternSwitchLabel,
																	SyntaxKind.CoalesceExpression,
																	SyntaxKind.CoalesceAssignmentExpression,
																	SyntaxKind.ConditionalExpression,
																	SyntaxKind.ConditionalAccessExpression,
																	SyntaxKind.LogicalAndExpression,
																	SyntaxKind.LogicalOrExpression,
																	SyntaxKind.AndPattern,
																	SyntaxKind.OrPattern
																};

			// private static readonly string[] LazyTypes = new[] { "System.Threading.Tasks.Task" };
			private readonly SemanticModel _semanticModel;
			private int _counter;

			public InnerComplexityAnalyzer(SemanticModel semanticModel)
				: base(SyntaxWalkerDepth.Node)
			{
				_semanticModel = semanticModel;
				_counter = 1;
			}

			public int Calculate(SyntaxNode syntax)
			{
				if (syntax != null)
				{
					Visit(syntax);
				}

				return _counter;
			}

			public override void Visit(SyntaxNode node)
			{
				base.Visit(node);
				if (Contributors.Contains(node.Kind()))
				{
					_counter++;
				}
			}

			public override void VisitWhileStatement(WhileStatementSyntax node)
			{
				base.VisitWhileStatement(node);
				_counter++;
			}

			public override void VisitForStatement(ForStatementSyntax node)
			{
				base.VisitForStatement(node);
				_counter++;
			}

			public override void VisitForEachStatement(ForEachStatementSyntax node)
			{
				base.VisitForEachStatement(node);
				_counter++;
			}

			/// <summary>
			/// Handles the deconstructing form, <c>foreach (var (a, b) in items)</c>. Roslyn models it as a
			/// separate node type, so without this override the loop would slip through uncounted.
			/// </summary>
			public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
			{
				base.VisitForEachVariableStatement(node);
				_counter++;
			}

			public override void VisitDoStatement(DoStatementSyntax node)
			{
				base.VisitDoStatement(node);
				_counter++;
			}

			//// TODO: Calculate for tasks
			////public override void VisitInvocationExpression(InvocationExpressionSyntax node)
			////{
			////	if (_semanticModel != null)
			////	{
			////		var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
			////		if (symbol != null)
			////		{
			////			switch (symbol.Kind)
			////			{
			////				case SymbolKind.Method:
			////					var returnType = ((IMethodSymbol)symbol).ReturnType;
			////					break;
			////			}
			////		}
			////	}
			////	base.VisitInvocationExpression(node);
			////}

			//// There is deliberately no VisitArgument override. An earlier version walked the body of a
			//// lambda argument explicitly and then let the base walker reach it again, so every branch
			//// inside a predicate was counted twice. Base traversal already covers lambda bodies.

			//// Nor is there an override for default(T), continue or goto. None of them chooses between
			//// paths: default(T) is a constant, and the two jumps are unconditional — the decision that
			//// leads to them belongs to the enclosing if, which is counted in its own right.

			public override void VisitIfStatement(IfStatementSyntax node)
			{
				base.VisitIfStatement(node);
				_counter++;
			}

			public override void VisitCatchClause(CatchClauseSyntax node)
			{
				base.VisitCatchClause(node);
				_counter++;
			}

			/// <summary>
			/// A <c>when</c> filter decides a second time, after the exception type has already matched, so
			/// it is a path of its own on top of the catch clause.
			/// </summary>
			public override void VisitCatchFilterClause(CatchFilterClauseSyntax node)
			{
				base.VisitCatchFilterClause(node);
				_counter++;
			}

			/// <summary>
			/// Counts each arm of a switch expression, mirroring how case labels are counted in a switch
			/// statement. The discard arm is skipped for the same reason <c>default:</c> is: it is the
			/// fall-through, not an extra decision.
			/// </summary>
			public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
			{
				base.VisitSwitchExpressionArm(node);
				if (!node.Pattern.IsKind(SyntaxKind.DiscardPattern))
				{
					_counter++;
				}
			}
		}
	}
}
