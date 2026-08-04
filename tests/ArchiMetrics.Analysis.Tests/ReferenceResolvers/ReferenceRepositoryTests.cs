// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ReferenceRepositoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ReferenceRepositoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Tests.ReferenceResolvers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.ReferenceResolvers;
    using Common;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Xunit;

    public sealed class ReferenceRepositoryTests
    {
        private ReferenceRepositoryTests()
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed in teardown.")]
        public class GivenAReferenceRepository : SolutionTestsBase, IDisposable
        {
            private readonly ReferenceRepository _sut;
            private readonly Solution _solution;

            public GivenAReferenceRepository()
            {
                const string Code = @"namespace Test
{
	using System;

	public class TestClass
	{
		private object _number = new object();

		public object GetNumber()
		{
			return _number;
		}
	}
}";
                _solution = CreateSolution(Code);
                _sut = new ReferenceRepository(_solution);
            }

            public void Dispose()
            {
                _sut.Dispose();
            }

            [Fact]
            public async Task WhenResolvingReferencesThenResolvesAllReferences()
            {
                var project = _solution.Projects.First();
                var compilation = await project.GetCompilationAsync();
                var document = project.Documents.First();
                var root = await document.GetSyntaxRootAsync();
                var model = compilation.GetSemanticModel(root.SyntaxTree);
                var symbol = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Select(x => model.GetDeclaredSymbol(x) as IMethodSymbol)
                    .Select(x => x.ReturnType)
                    .First();

                await _sut.EnsureScanned();

                var location = _sut.Get(symbol).AsArray();

                Assert.Equal(3, location.Length);
            }
        }

        /// <summary>
        /// The repository is the shared, build-once index that every reference-hungry rule reads
        /// from. A real solution spans several projects, so the index is only trustworthy if a type
        /// declared in one project can still be found when it is used from another. If it cannot,
        /// every rule built on top of it silently under-reports: unused-code rules call live code
        /// dead, and coupling rules see a type as unreferenced simply because its callers live
        /// next door.
        /// </summary>
        public class GivenASolutionWithMultipleProjects
        {
            [Fact]
            public async Task WhenTypeIsUsedFromAnotherProjectThenResolvesTheReference()
            {
                var workspace = new AdhocWorkspace();
                var libraryId = ProjectId.CreateNewId("library");
                var consumerId = ProjectId.CreateNewId("consumer");
                var runtime = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

                var solution = workspace.CurrentSolution
                    .AddProject(libraryId, "library", "library.dll", LanguageNames.CSharp)
                    .AddMetadataReference(libraryId, runtime)
                    .AddDocument(
                        DocumentId.CreateNewId(libraryId),
                        "Widget.cs",
                        "namespace Lib { public class Widget { public void Use() { } } }")
                    .AddProject(consumerId, "consumer", "consumer.dll", LanguageNames.CSharp)
                    .AddMetadataReference(consumerId, runtime)
                    .AddProjectReference(consumerId, new ProjectReference(libraryId))
                    .AddDocument(
                        DocumentId.CreateNewId(consumerId),
                        "Consumer.cs",
                        "namespace App { using Lib; public class Consumer { public void Run() { new Widget().Use(); } } }");

                using (var repository = new ReferenceRepository(solution))
                {
                    await repository.EnsureScanned();

                    var libraryCompilation = await solution.GetProject(libraryId).GetCompilationAsync();
                    var widget = libraryCompilation.GetTypeByMetadataName("Lib.Widget");

                    var locations = repository.Get(widget).AsArray();

                    Assert.NotEmpty(locations);
                }
            }
        }
    }
}
