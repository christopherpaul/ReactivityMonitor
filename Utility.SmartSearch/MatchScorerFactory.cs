using System;
using System.Collections.Generic;
using System.Text;

namespace Utility.SmartSearch
{
    public sealed class MatchScorerFactory : IMatchScorerFactory
    {
        private readonly NormalizationForm mNormalizationForm;

        public static IMatchScorerFactory Default { get; } = new MatchScorerFactory(NormalizationForm.FormKD);

        public MatchScorerFactory(NormalizationForm normalizationForm)
        {
            mNormalizationForm = normalizationForm;
        }

        public IMatchScorer Create(string searchText)
        {
            return new Scorer(searchText, mNormalizationForm);
        }
    }
}
