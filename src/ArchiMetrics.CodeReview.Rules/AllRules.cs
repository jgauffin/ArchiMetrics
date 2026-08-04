// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AllRules.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the AllRules type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Analysis.Common;
	using Analysis.Common.CodeReview;

    /// <summary>
	/// The entry point to this library: discovers every review rule it contains and hands them to a
	/// <c>NodeReviewer</c>.
	/// </summary>
	/// <remarks>
	/// Rules are found by reflection rather than listed here, so adding a rule is a matter of writing the
	/// class and nothing else — there is no registry to forget to update. The individual rule types stay
	/// internal for the same reason: callers choose which <em>sets</em> of rules to run, not which concrete
	/// classes, which leaves the library free to add, split or rename rules without breaking anyone.
	/// </remarks>
	public static class AllRules
	{
		/// <summary>
		/// Gets every syntax rule in the library — the rules that inspect code shape without needing type
		/// information, and so the cheaper of the two sets to run.
		/// </summary>
		/// <param name="spellChecker">
		/// The spell checker handed to rules that examine identifiers and comments for real words. May be
		/// <see langword="null"/>, in which case those rules are still created but have nothing to check
		/// against — pass one if you want naming and comment-language rules to do anything.
		/// </param>
		/// <returns>The rules, ordered by <c>ID</c> so that results are stable between runs.</returns>
		public static IEnumerable<ISyntaxEvaluation> GetSyntaxRules(ISpellChecker spellChecker)
		{
			var types = (from type in typeof(AllRules).Assembly.GetTypes()
						 where typeof(ISyntaxEvaluation).IsAssignableFrom(type)
						 where !type.IsInterface && !type.IsAbstract
						 select type).AsArray();
			var simple =
				types.Where(x => x.GetConstructors().Any(c => c.GetParameters().Length == 0))
					.Select(Activator.CreateInstance)
					.Cast<ISyntaxEvaluation>();
			var spelling =
				types.Where(
					x =>
					x.GetConstructors()
						.Any(
							c => c.GetParameters().Length == 1 && typeof(ISpellChecker).IsAssignableFrom(c.GetParameters()[0].ParameterType)))
					.Select(x => Activator.CreateInstance(x, spellChecker))
					.Cast<ISyntaxEvaluation>();

			return simple.Concat(spelling).OrderBy(x => x.ID).AsArray();
		}

		/// <summary>
		/// Gets every symbol rule in the library — the rules that need a resolved semantic model, and so
		/// can reason about what a name actually refers to rather than only how the code is written.
		/// </summary>
		/// <remarks>
		/// These cost more to run than the syntax rules because they require a compilation, which is why the
		/// two sets are exposed separately: a caller doing a quick pass can take the syntax rules alone.
		/// </remarks>
		/// <returns>The rules, one instance of each parameterless type found in the assembly.</returns>
		public static IEnumerable<ISymbolEvaluation> GetSymbolRules()
		{
			var types = from type in typeof(AllRules).Assembly.GetTypes()
				   where typeof(ISymbolEvaluation).IsAssignableFrom(type)
				   where !type.IsInterface && !type.IsAbstract
				   select type;

			var simple =
				types.Where(x => x.GetConstructors().Any(c => c.GetParameters().Length == 0))
					.Select(Activator.CreateInstance)
					.Cast<ISymbolEvaluation>();

			return simple.AsArray();
		}
	}
}
