// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SymbolExtensions.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the SymbolExtensions type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using ArchiMetrics.Analysis.ReferenceResolvers;
	using Microsoft.CodeAnalysis;

	public static class SymbolExtensions
	{
		/// <summary>
		/// One reference index per solution snapshot.
		/// <para>
		/// The table is keyed on the solution instance itself, which gives two properties for free.
		/// Roslyn treats a solution as immutable, so editing produces a new instance and therefore a
		/// new index - keying on the solution *id* instead would survive edits and hand back stale
		/// references. And because the table holds its keys weakly, an index disappears once the
		/// solution it describes is collected, so a long-lived host such as the analysis agent does
		/// not accumulate every solution it has ever opened.
		/// </para>
		/// </summary>
		private static readonly ConditionalWeakTable<Solution, Lazy<ReferenceRepository>> KnownReferences = new ConditionalWeakTable<Solution, Lazy<ReferenceRepository>>();

		/// <summary>
		/// Builds the solution's reference index if it does not exist yet.
		/// <para>
		/// Call this once before analysing many symbols. The index is what makes reference lookups
		/// cheap, but building it is the single most expensive step, so it is better paid once up
		/// front than discovered concurrently by hundreds of callers.
		/// </para>
		/// </summary>
		public static Task WarmReferenceIndex(this Solution solution)
		{
			return solution == null
				? Task.CompletedTask
				: GetRepository(solution).EnsureScanned();
		}

		/// <summary>
		/// Looks a symbol up in the solution's shared reference index, building that index on first
		/// use.
		/// <para>
		/// Rules call this once per declaration, so a solution produces tens of thousands of
		/// lookups. That makes the cost of a single lookup the thing that matters: this used to
		/// wrap every one in a <c>Task.Run</c> that then blocked waiting for the index, so
		/// thousands of pool threads sat blocked on work that itself needed pool threads to finish,
		/// and the analysis stalled rather than ran. Awaiting the scan once and then reading
		/// straight from the dictionary keeps a lookup as cheap as it looks.
		/// </para>
		/// </summary>
		public static async Task<ReferencedSymbol> FindReferences(this Solution solution, ISymbol symbol)
		{
			if (solution == null)
			{
				return new ReferencedSymbol(symbol, new ReferenceLocation[0]);
			}

			var repository = GetRepository(solution);

			await repository.EnsureScanned().ConfigureAwait(false);

			return new ReferencedSymbol(symbol, repository.Get(symbol));
		}

		private static ReferenceRepository GetRepository(Solution solution)
		{
			return KnownReferences.GetValue(
				solution,
				x => new Lazy<ReferenceRepository>(() => new ReferenceRepository(x), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
		}
	}
}
