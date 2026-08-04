// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PhysicalLinesCalculator.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the PhysicalLinesCalculator type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Metrics
{
	using System.Collections.Generic;
	using Microsoft.CodeAnalysis;

	/// <summary>
	/// Counts the source lines a piece of code actually occupies — the size a reader perceives on opening
	/// the file.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only lines carrying real code are counted. Blank lines and comment-only lines contain no tokens, so
	/// they drop out naturally, and leading trivia is excluded, which keeps XML documentation from inflating
	/// the size of the member it documents. Lines holding nothing but a brace <em>are</em> counted: they are
	/// part of what a reader scrolls past, and pretending otherwise would make the number something other
	/// than a line count.
	/// </para>
	/// <para>
	/// Use this for anything the reader experiences as bulk — report densities, KLOC denominators, "how big
	/// is this file". Do not use it to drive the maintainability index: reformatting code would then change
	/// its measured maintainability, which is exactly the trap this type exists to keep separate from
	/// <see cref="ExecutableStatementsCalculator"/>.
	/// </para>
	/// </remarks>
	internal sealed class PhysicalLinesCalculator
	{
		/// <summary>
		/// Counts the distinct source lines spanned by the node's tokens.
		/// </summary>
		/// <param name="node">The node to measure. May be <see langword="null"/>.</param>
		/// <returns>The number of lines containing code, or zero if there is nothing to measure.</returns>
		public int Calculate(SyntaxNode node)
		{
			var tree = node?.SyntaxTree;
			if (tree == null)
			{
				return 0;
			}

			var text = tree.GetText();
			var lines = new HashSet<int>();

			// Walking tokens rather than trivia is what excludes comments and blank lines: neither produces
			// a token, so neither can add a line to the set.
			foreach (var token in node.DescendantTokens())
			{
				if (token.Span.IsEmpty)
				{
					continue;
				}

				var first = text.Lines.GetLineFromPosition(token.SpanStart).LineNumber;
				var last = text.Lines.GetLineFromPosition(token.Span.End).LineNumber;

				// A token can straddle lines — a verbatim or raw string literal, for instance — and every
				// line it covers is occupied by code.
				for (var line = first; line <= last; line++)
				{
					lines.Add(line);
				}
			}

			return lines.Count;
		}
	}
}
