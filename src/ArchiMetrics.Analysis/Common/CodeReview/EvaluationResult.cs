// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EvaluationResult.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the EvaluationResult type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.CodeReview
{
    using System.Collections.Generic;

    /// <summary>
	/// A single finding from a code review rule: what was found, where, and how much it matters.
	/// </summary>
	/// <remarks>
	/// A rule returns one of these when it matches and <see langword="null"/> when it does not, so the
	/// presence of the object is itself the finding. The location fields are filled in by the rule base
	/// classes after the rule has run, which is why they are settable rather than passed to a constructor.
	/// </remarks>
	public class EvaluationResult
	{
		/// <summary>
		/// Gets or sets the name of the project containing the finding.
		/// </summary>
		public string ProjectName { get; set; }

		/// <summary>
		/// Gets or sets the path to the project file.
		/// </summary>
		public string ProjectPath { get; set; }

		/// <summary>
		/// Gets or sets the namespace containing the finding.
		/// </summary>
		public string Namespace { get; set; }

		/// <summary>
		/// Gets or sets the name of the type containing the finding.
		/// </summary>
		public string TypeName { get; set; }

		/// <summary>
		/// Gets or sets the kind of that type — class, interface, struct and so on.
		/// </summary>
		public string TypeKind { get; set; }

		/// <summary>
		/// Gets or sets the path to the source file containing the finding.
		/// </summary>
		public string FilePath { get; set; }

		/// <summary>
		/// Gets or sets the rule's short description of what is wrong.
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or sets the rule's advice on what to do about it. This is what makes a finding actionable
		/// rather than merely a complaint.
		/// </summary>
		public string Suggestion { get; set; }

		/// <summary>
		/// Gets or sets the offending source, so a reader can judge the finding without opening the file.
		/// </summary>
		public string Snippet { get; set; }

		/// <summary>
		/// Gets or sets how many source lines the finding covers.
		/// </summary>
		/// <remarks>
		/// Physical lines, excluding blanks, comments and documentation. This measures the size of the
		/// affected region, not the severity of the problem.
		/// </remarks>
		public int LinesOfCodeAffected { get; set; }

		/// <summary>
		/// Gets or sets how many times the rule matched within the inspected node. Defaults to 1 when the
		/// rule does not count occurrences itself.
		/// </summary>
		public int ErrorCount { get; set; }

		/// <summary>
		/// Gets or sets how serious the finding is. Note this scale runs the opposite way to the metrics —
		/// lower is worse.
		/// </summary>
		public CodeQuality Quality { get; set; }

		/// <summary>
		/// Gets or sets which qualities of the code the finding harms. Several may apply at once.
		/// </summary>
		public QualityAttribute QualityAttribute { get; set; }

		/// <summary>
		/// Gets or sets how much code the finding affects, from a whole project down to one expression.
		/// </summary>
		public ImpactLevel ImpactLevel { get; set; }

		/// <summary>
		/// CWE identifiers mapped to this violation, if the originating rule
		/// implements <see cref="ICweMapping"/>. Null when the rule has no CWE mapping.
		/// </summary>
		public IReadOnlyList<string> CweIds { get; set; }

		/// <summary>
		/// The ISO/IEC 5055 category this violation belongs to, if the originating rule
		/// implements <see cref="ICweMapping"/>. Null when the rule has no CWE mapping.
		/// </summary>
		public Iso5055Category? Iso5055Category { get; set; }
	}
}
