// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExecutableStatementsCalculator.cs" company="Reimers.dk">
//   Copyright © Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ExecutableStatementsCalculator type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Metrics
{
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	/// <summary>
	/// Counts the executable statements in a piece of code — the units of work it actually performs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a count of syntax constructs, not of text. Formatting cannot change it: a statement wrapped
	/// over five lines still counts once, and blank lines, comments and braces count for nothing. That
	/// property is why this, rather than physical lines, is the size input to the maintainability index —
	/// reformatting code must not change its measured maintainability.
	/// </para>
	/// <para>
	/// For the size a reader actually perceives when opening a file, use
	/// <see cref="PhysicalLinesCalculator"/> instead.
	/// </para>
	/// </remarks>
	internal sealed class ExecutableStatementsCalculator
	{
		public int Calculate(SyntaxNode node)
		{
			var innerCalculator = new InnerExecutableStatementsCalculator();
			return innerCalculator.Calculate(node);
		}

		private class InnerExecutableStatementsCalculator : CSharpSyntaxWalker
		{
			private int _counter;

			public InnerExecutableStatementsCalculator()
				: base(SyntaxWalkerDepth.Node)
			{
			}

			public int Calculate(SyntaxNode node)
			{
				if (node != null)
				{
					Visit(node);
				}

				return _counter;
			}

			public override void VisitCheckedStatement(CheckedStatementSyntax node)
			{
				base.VisitCheckedStatement(node);
				_counter++;
			}

			public override void VisitDoStatement(DoStatementSyntax node)
			{
				base.VisitDoStatement(node);
				_counter++;
			}

			public override void VisitEmptyStatement(EmptyStatementSyntax node)
			{
				base.VisitEmptyStatement(node);
				_counter++;
			}

			public override void VisitExpressionStatement(ExpressionStatementSyntax node)
			{
				base.VisitExpressionStatement(node);
				_counter++;
			}

			/// <summary>
			/// Called when the visitor visits a AccessorDeclarationSyntax node.
			/// </summary>
			public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
			{
				if (node.Body == null)
				{
					_counter++;
				}

				base.VisitAccessorDeclaration(node);
			}

			public override void VisitFixedStatement(FixedStatementSyntax node)
			{
				base.VisitFixedStatement(node);
				_counter++;
			}

			public override void VisitForEachStatement(ForEachStatementSyntax node)
			{
				base.VisitForEachStatement(node);
				_counter++;
			}

			public override void VisitForStatement(ForStatementSyntax node)
			{
				base.VisitForStatement(node);
				_counter++;
			}

			public override void VisitGlobalStatement(GlobalStatementSyntax node)
			{
				base.VisitGlobalStatement(node);
				_counter++;
			}

			public override void VisitGotoStatement(GotoStatementSyntax node)
			{
				base.VisitGotoStatement(node);
				_counter++;
			}

			public override void VisitIfStatement(IfStatementSyntax node)
			{
				base.VisitIfStatement(node);
				_counter++;
			}

			/// <summary>
			/// Counts an initializer as the single statement it is, however many elements it holds.
			/// </summary>
			/// <remarks>
			/// This used to add one per element, so a hundred-entry lookup table measured as a hundred
			/// statements. Because the maintainability index subtracts <c>16.2 * ln(size)</c>, that
			/// collapsed the score of code which is in truth trivial to maintain — a table has no branches
			/// and no logic to follow. Size should reflect work done, and building one collection is one
			/// piece of work.
			/// </remarks>
			public override void VisitInitializerExpression(InitializerExpressionSyntax node)
			{
				base.VisitInitializerExpression(node);
				_counter++;
			}

			public override void VisitLabeledStatement(LabeledStatementSyntax node)
			{
				base.VisitLabeledStatement(node);
				_counter++;
			}

			/// <summary>
			/// Counts local declarations, except <c>const</c> ones. A constant is resolved by the compiler
			/// and performs no work at run time, so charging it as a statement would penalise naming a
			/// magic number — the opposite of what this tool should encourage.
			/// </summary>
			public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
			{
				base.VisitLocalDeclarationStatement(node);
				if (!node.Modifiers.Any(SyntaxKind.ConstKeyword))
				{
					_counter++;
				}
			}

			public override void VisitLockStatement(LockStatementSyntax node)
			{
				base.VisitLockStatement(node);
				_counter++;
			}

			public override void VisitReturnStatement(ReturnStatementSyntax node)
			{
				base.VisitReturnStatement(node);
				if (node.Expression != null)
				{
					_counter++;
				}
			}

			public override void VisitSwitchStatement(SwitchStatementSyntax node)
			{
				base.VisitSwitchStatement(node);
				_counter++;
			}

			public override void VisitThrowStatement(ThrowStatementSyntax node)
			{
				base.VisitThrowStatement(node);
				_counter++;
			}

			public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
			{
				base.VisitUnsafeStatement(node);
				_counter++;
			}

			public override void VisitUsingStatement(UsingStatementSyntax node)
			{
				base.VisitUsingStatement(node);
				_counter++;
			}

			/// <summary>
			/// Called when the visitor visits a ConstructorDeclarationSyntax node.
			/// </summary>
			public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
			{
				base.VisitConstructorDeclaration(node);
				_counter++;
			}

			public override void VisitWhileStatement(WhileStatementSyntax node)
			{
				base.VisitWhileStatement(node);
				_counter++;
			}

			public override void VisitYieldStatement(YieldStatementSyntax node)
			{
				base.VisitYieldStatement(node);
				_counter++;
			}
		}
	}
}
