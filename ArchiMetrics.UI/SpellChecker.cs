namespace ArchiMetrics.UI
{
	using System;
	using Common;
	using NHunspell;

	internal class SpellChecker : ISpellChecker
	{
		private readonly Hunspell _speller;

		public SpellChecker(Hunspell speller)
		{
			_speller = speller;
		}

		public bool Spell(string word)
		{
			return _speller.Spell(word);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~SpellChecker()
		{
			Dispose(false);
		}

		protected virtual void Dispose(bool isDisposing)
		{
			if (isDisposing)
			{
				_speller.Dispose(true);
			}
		}
	}
}