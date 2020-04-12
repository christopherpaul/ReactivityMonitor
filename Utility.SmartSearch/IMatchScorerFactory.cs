using System;
using System.Collections.Generic;
using System.Text;

namespace Utility.SmartSearch
{
    public interface IMatchScorerFactory
    {
        /// <summary>
        /// Creates a <see cref="IMatchScorer"/> for scoring candidates against
        /// the specified <paramref name="searchText"/>.
        /// </summary>
        IMatchScorer Create(string searchText);
    }
}
