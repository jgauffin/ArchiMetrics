// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SpellChecker.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the SpellChecker type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.CodeReview.Rules.Tests.Rules
{
	using System;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using Analysis.Common.CodeReview;
	using WeCantSpell.Hunspell;

	internal class SpellChecker : ISpellChecker
	{
		private readonly IKnownPatterns _knownPatterns;
		private readonly WordList _speller;

		public SpellChecker(IKnownPatterns knownPatterns)
		{
			_knownPatterns = knownPatterns;
			using (var archive = ZipFile.OpenRead(@"Dictionaries\dict-en.oxt"))
			{
				var affEntry = archive.Entries.FirstOrDefault(z => z.FullName == "en_US.aff");
				var dicEntry = archive.Entries.FirstOrDefault(z => z.FullName == "en_US.dic");
				using (var affStream = affEntry.Open())
				using (var dicStream = dicEntry.Open())
				{
					_speller = WordList.CreateFromStreams(dicStream, affStream);
				}
			}
		}

		~SpellChecker()
		{
			Dispose(false);
		}

		public bool Spell(string word)
		{
			return _knownPatterns.IsExempt(word) || _speller.Check(word);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool isDisposing)
		{
		}
	}
}
