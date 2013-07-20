// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TypeDeclarationSyntaxInfo.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2012
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the TypeDeclarationSyntaxInfo type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace ArchiMetrics.Common.Metrics
{
	using Roslyn.Compilers.Common;

	public class TypeDeclarationSyntaxInfo
	{
		public TypeDeclarationSyntaxInfo(string codeFile, string name, CommonSyntaxNode syntax)
		{
			CodeFile = codeFile;
			Name = name;
			Syntax = syntax;
		}

		public string CodeFile { get; private set; }

		public string Name { get; private set; }

		public CommonSyntaxNode Syntax { get; set; }
	}
}