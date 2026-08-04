// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ReferenceRepository.cs" company="Reimers.dk">
//   Copyright © Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ReferenceRepository type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.ReferenceResolvers
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Common;
	using Microsoft.CodeAnalysis;

	/// <summary>
	/// A reverse index of "which code refers to this symbol?", built once for a whole solution.
	/// <para>
	/// Answering that question on demand means asking Roslyn to bind the solution again for every
	/// symbol, which is quadratic: a few hundred classes times a few hundred documents. Binding
	/// every node exactly once up front and remembering the answers turns each later question into
	/// a dictionary lookup, which is the difference between minutes and seconds for a real
	/// solution.
	/// </para>
	/// <para>
	/// Callers must await <see cref="EnsureScanned"/> before calling <see cref="Get"/>. Get is a
	/// plain synchronous read on purpose: an earlier version blocked inside Get, and because every
	/// rule performs thousands of lookups, those blocked threads exhausted the thread pool and the
	/// analysis appeared to hang. Waiting is now something the caller does once, asynchronously,
	/// rather than something every lookup does implicitly.
	/// </para>
	/// </summary>
	public class ReferenceRepository : IProvider<ISymbol, IEnumerable<ReferenceLocation>>
	{
		private readonly ConcurrentDictionary<ISymbol, IEnumerable<ReferenceLocation>> _resolvedReferences = new ConcurrentDictionary<ISymbol, IEnumerable<ReferenceLocation>>();
		private readonly Task _scanTask;

		public ReferenceRepository(Solution solution)
		{
			_scanTask = Scan(solution);
		}

		/// <summary>
		/// Completes once the solution has been indexed. Await this before the first
		/// <see cref="Get"/>; afterwards it completes synchronously, so callers pay nothing.
		/// </summary>
		public Task EnsureScanned()
		{
			return _scanTask;
		}

		/// <summary>
		/// Returns the known references for a symbol. Only meaningful after
		/// <see cref="EnsureScanned"/> has completed - before that the index is still filling and
		/// this will under-report.
		/// </summary>
		public IEnumerable<ReferenceLocation> Get(ISymbol key)
		{
			IEnumerable<ReferenceLocation> locations;
			return _resolvedReferences.TryGetValue(key, out locations)
				? locations
				: Enumerable.Empty<ReferenceLocation>();
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool isDisposing)
		{
			if (isDisposing)
			{
				_resolvedReferences.Clear();
			}
		}

		private async Task Scan(Solution solution)
		{
			var roots = await GetDocData(solution).ConfigureAwait(false);

			var documents = roots
				.SelectMany(data => data.DocRoots.Select(docRoot => new { data.Compilation, Root = docRoot }))
				.AsArray();

			// Binding is the expensive half of the work and each document is independent, so it is
			// spread across cores. Results accumulate into queues rather than arrays: appending to
			// a queue is O(1), whereas the previous "concat into a new array" grew quadratically
			// for symbols referenced from many documents - exactly the widely used types that
			// dominate a real solution.
			var pending = new ConcurrentDictionary<ISymbol, ConcurrentQueue<ReferenceLocation>>();

			await Parallel.ForEachAsync(
				documents,
				new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
				(document, _) =>
				{
					foreach (var @group in document.Compilation.Resolve(document.Root))
					{
						var locations = pending.GetOrAdd(@group.Key, _ => new ConcurrentQueue<ReferenceLocation>());
						foreach (var location in @group)
						{
							locations.Enqueue(location);
						}
					}

					return ValueTask.CompletedTask;
				}).ConfigureAwait(false);

			foreach (var entry in pending)
			{
				_resolvedReferences[entry.Key] = entry.Value.AsArray();
			}
		}

		private async Task<IEnumerable<DocData>> GetDocData(Solution solution)
		{
			var roots = (from project in solution.Projects
						 let compilation = project.GetCompilationAsync()
						 let docRoots = project.Documents.Select(x => x.GetSyntaxRootAsync())
						 select new { compilation, docRoots }).AsArray();

			await Task.WhenAll(roots.SelectMany(x => new Task[] { x.compilation }.Concat(x.docRoots))).ConfigureAwait(false);

			return roots.Select(x => new DocData
			{
				Compilation = x.compilation.Result,
				DocRoots = x.docRoots.Select(y => y.Result).AsArray()
			});
		}

		private class DocData
		{
			public Compilation Compilation { get; set; }

			public IEnumerable<SyntaxNode> DocRoots { get; set; }
		}
	}
}
