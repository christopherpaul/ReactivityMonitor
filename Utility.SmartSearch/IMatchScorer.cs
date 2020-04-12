using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Utility.SmartSearch
{
    public interface IMatchScorer
    {
        /// <summary>
        /// Scores the specified <paramref name="candidate"/>.
        /// </summary>
        int Score(string candidate);

        /// <summary>
        /// Returns the character positions of <paramref name="candidate"/> that give the best match.
        /// Note that these refer to a normalized version of the candidate string, which is assigned
        /// to <paramref name="candidate"/> on output. If the candidate does not match, returns an
        /// empty array.
        /// </summary>
        ImmutableArray<int> GetMatchPositions(ref string candidate);
    }
}
