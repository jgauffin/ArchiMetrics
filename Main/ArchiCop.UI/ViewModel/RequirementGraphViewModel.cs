// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RequirementGraphViewModel.cs" company="Roche">
//   Copyright � Roche 2012
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993] for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the RequirementGraphViewModel type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMeter.UI.ViewModel
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Threading.Tasks;

	using ArchiMeter.Analysis;
	using ArchiMeter.Common;

	internal class RequirementGraphViewModel : ViewModelBase
	{
		private readonly IRequirementTestAnalyzer _analyzer;
		private readonly ISolutionEdgeItemsRepositoryConfig _config;
		private readonly IEdgeTransformer _filter;
		private EdgeItem[] _allEdges;
		private ProjectGraph _graphToVisualize;

		public RequirementGraphViewModel(IRequirementTestAnalyzer analyzer, ISolutionEdgeItemsRepositoryConfig config, IEdgeTransformer filter)
		{
			this._analyzer = analyzer;
			this._config = config;
			this._filter = filter;
			this.LoadAllEdges();
		}

		public ProjectGraph GraphToVisualize
		{
			get
			{
				return this._graphToVisualize;
			}

			private set
			{
				if (this._graphToVisualize != value)
				{
					this._graphToVisualize = value;
					this.RaisePropertyChanged();
				}
			}
		}

		public void Update(bool forceUpdate)
		{
			if (forceUpdate)
			{
				this.LoadAllEdges();
			}
			else
			{
				this.UpdateInternal();
			}
		}

		private async void UpdateInternal()
		{
			this.IsLoading = true;
			var g = new ProjectGraph();

			var nonEmptySourceItems = (await this._filter.TransformAsync(this._allEdges))
				.ToArray();

			var projectVertices = nonEmptySourceItems
				.SelectMany(item => this.CreateVertices(item)
										.GroupBy(v => v.Name)
										.Select(grouping => grouping.First()))
				.ToArray();

			var edges =
				nonEmptySourceItems
				.Where(e => !string.IsNullOrWhiteSpace(e.Dependency))
				.Select(
					dependencyItemViewModel =>
					new ProjectEdge(
						projectVertices.First(item => item.Name == dependencyItemViewModel.Dependant), 
						projectVertices.First(item => item.Name == dependencyItemViewModel.Dependency)))
								   .Where(e => e.Target.Name != e.Source.Name)
								   .ToList();

			foreach (var vertex in projectVertices)
			{
				g.AddVertex(vertex);
			}

			foreach (var edge in edges)
			{
				g.AddEdge(edge);
			}

			this.GraphToVisualize = g;
			this.IsLoading = false;
		}

		private IEnumerable<Vertex> CreateVertices(EdgeItem item)
		{
			yield return new Vertex(item.Dependant, false, item.DependantComplexity, item.DependantMaintainabilityIndex, item.DependantLinesOfCode);
			if (!string.IsNullOrWhiteSpace(item.Dependency))
			{
				yield return
					new Vertex(item.Dependency, false, item.DependencyComplexity, item.DependencyMaintainabilityIndex, item.DependencyLinesOfCode, item.CodeIssues);
			}
		}

		private async void LoadAllEdges()
		{
			this.IsLoading = true;
			var edges = await Task.Factory.StartNew(() => this._analyzer.GetTestData(this._config.Path));
			this._allEdges = await Task.Factory.StartNew(() => edges.SelectMany(this.ConvertToEdgeItem).Where(e => e.Dependant != e.Dependency).Distinct(new RequirementsEqualityComparer()).ToArray());
			this.UpdateInternal();
		}

		private IEnumerable<EdgeItem> ConvertToEdgeItem(TestData data)
		{
			return data.RequirementIds.SelectMany(
				i => data.RequirementIds.Except(new[]
												{
													i
												})
						 .Select(
							 o =>
							 new EdgeItem
							 {
								 Dependant = i.ToString(CultureInfo.InvariantCulture), 
								 Dependency = o.ToString(CultureInfo.InvariantCulture), 
								 CodeIssues = new EvaluationResult[0]
							 }));
		}

		private class RequirementsEqualityComparer : IEqualityComparer<EdgeItem>
		{
			public bool Equals(EdgeItem x, EdgeItem y)
			{
				return x == null
						   ? y == null
						   : y != null && ((x.Dependant == y.Dependant && x.Dependency == y.Dependency) || (x.Dependant == y.Dependency && x.Dependency == y.Dependant));
			}

			public int GetHashCode(EdgeItem obj)
			{
				return string.Join(";", new[] { obj.Dependant, obj.Dependency }.OrderBy(x => x)).GetHashCode();
			}
		}
	}
}