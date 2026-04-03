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

    public class EvaluationResult
	{
		public string ProjectName { get; set; }

		public string ProjectPath { get; set; }

		public string Namespace { get; set; }

		public string TypeName { get; set; }

		public string TypeKind { get; set; }

		public string FilePath { get; set; }

		public string Title { get; set; }

		public string Suggestion { get; set; }

		public string Snippet { get; set; }

		public int LinesOfCodeAffected { get; set; }

		public int ErrorCount { get; set; }

		public CodeQuality Quality { get; set; }

		public QualityAttribute QualityAttribute { get; set; }

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
