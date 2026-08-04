// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AccessModifierKind.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the AccessModifierKind type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System;

    /// <summary>
	/// The declared accessibility of a type or member.
	/// </summary>
	/// <remarks>
	/// A flags enum because C# allows combinations such as <c>protected internal</c>. Accessibility is worth
	/// having alongside the metrics: the same complexity matters more in a public member, which the outside
	/// world depends on and which cannot be changed freely, than in a private one.
	/// </remarks>
	[Flags]
	public enum AccessModifierKind
	{
		/// <summary>Visible only within the declaring type.</summary>
		Private = 1,

		/// <summary>Visible to the declaring type and anything deriving from it.</summary>
		Protected = 2,

		/// <summary>Visible to everyone, and so part of the assembly's contract.</summary>
		Public = 4,

		/// <summary>Visible within the declaring assembly.</summary>
		Internal = 8
	}
}