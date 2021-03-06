// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CodeQuality.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the CodeQuality type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Common.CodeReview
{
	public enum CodeQuality
	{
		Broken = 0, 
		NeedsReEngineering = 1, 
		NeedsRefactoring = 2, 
		NeedsCleanup = 3, 
		NeedsReview = 4, 
		Good = 5
	}
}
