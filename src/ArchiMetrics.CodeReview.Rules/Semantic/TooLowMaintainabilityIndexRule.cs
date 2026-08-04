// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TooLowMaintainabilityIndexRule.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the TooLowMaintainabilityIndexRule type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Semantic
{
    using System.Threading.Tasks;
    using Analysis.Common;
    using Analysis.Common.CodeReview;
    using Analysis.Common.Metrics;
    using ArchiMetrics.Analysis.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    /// <summary>
    /// Flags methods whose maintainability index has fallen far enough that changing them is likely
    /// to be slow and error-prone.
    /// </summary>
    /// <remarks>
    /// The trigger point comes from <see cref="MetricThresholds.Maintainability.NeedsRefactoring"/>
    /// rather than a literal in this file. Sharing it with the reporting side is what stops the tool
    /// from contradicting itself — a summary describing a method as merely "Concerning" while this
    /// rule reports the same method as a defect would leave the reader with no idea which to trust.
    /// </remarks>
    internal class TooLowMaintainabilityIndexRule : SemanticEvaluationBase
    {
        public TooLowMaintainabilityIndexRule()
            : this(MetricThresholds.Maintainability.NeedsRefactoring)
        {
        }

        /// <summary>
        /// Initialises the rule with a custom trigger point, for callers who want to be stricter or
        /// more lenient than the library default.
        /// </summary>
        /// <param name="threshold">
        /// A maintainability index on the 0-100 scale. Methods at or below it are reported.
        /// </param>
        public TooLowMaintainabilityIndexRule(int threshold)
        {
            Threshold = threshold;
        }

        public override string ID => "AM0058";

        public override SyntaxKind EvaluatedKind => SyntaxKind.MethodDeclaration;

        public override string Title => "Method Unmaintainable";

        public override string Suggestion => "Refactor method to improve maintainability.";

        public override CodeQuality Quality => CodeQuality.NeedsRefactoring;

        public override QualityAttribute QualityAttribute => QualityAttribute.Testability | QualityAttribute.Maintainability | QualityAttribute.Modifiability;

        public override ImpactLevel ImpactLevel => ImpactLevel.Member;

        /// <summary>
        /// Gets the maintainability index at or below which a method is reported. On the 0-100
        /// maintainability scale, where higher is better.
        /// </summary>
        public int Threshold { get; }

        protected override Task<EvaluationResult> EvaluateImpl(SyntaxNode node, SemanticModel semanticModel, Solution solution)
        {
            var counter = new MemberMetricsCalculator(semanticModel, solution, solution.FilePath.GetParentFolder(), new MemberDocumentationFactory());

            var methodDeclaration = (MethodDeclarationSyntax)node;
            var metric = counter.CalculateSlim(methodDeclaration);
            return metric.MaintainabilityIndex <= Threshold
                       ? Task.FromResult(
                           new EvaluationResult
                           {
                               Snippet = node.ToFullString()
                           })
                       : Task.FromResult((EvaluationResult)null);
        }
    }
}