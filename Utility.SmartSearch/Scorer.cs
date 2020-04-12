using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Utility.SmartSearch
{
    internal sealed class Scorer : IMatchScorer
    {
        private readonly string mSearchString;
        private readonly JustScoreState[] mJustScoreStates;
        private readonly ScoreAndMatchPositionsState[] mScoreAndMatchPositionsStates;
        private readonly NormalizationForm mNormalizationForm;

        /// <summary>
        /// Private interface so we can use same method for score-only and for
        /// getting the matching character positions.
        /// </summary>
        private interface IState<TSelf> where TSelf : struct, IState<TSelf>
        {
            void Init(int stateIndex);
            void UpdateAssumingNoMatch();
            bool UpdateMatch(TSelf prevState, int charMatchScore, int consecutiveMatchBonus, int charPosition);
            TSelf NoMatch { get; }
        }

        private struct JustScoreState : IState<JustScoreState>
        {
            private int mScorePreviousMatched;
            private int mScorePreviousNonMatched;
            private int mConsecutiveMatchBonus;

            private static readonly JustScoreState cNoMatch;

            static JustScoreState()
            {
                cNoMatch.mScorePreviousNonMatched = int.MinValue;
                cNoMatch.mScorePreviousMatched = int.MinValue;
            }

            public void Init(int stateIndex)
            {
                this = default;
            }

            public void UpdateAssumingNoMatch()
            {
                mScorePreviousNonMatched = Math.Max(mScorePreviousMatched, mScorePreviousNonMatched);
                mScorePreviousMatched = 0;
                mConsecutiveMatchBonus = 0;
            }

            public bool UpdateMatch(JustScoreState prevState, int charMatchScore, int consecutiveMatchBonus, int charPosition)
            {
                int nextScorePreviousMatched = prevState.mScorePreviousMatched + charMatchScore + prevState.mConsecutiveMatchBonus;
                int nextScorePreviousNonMatched = prevState.mScorePreviousNonMatched + charMatchScore;

                if (nextScorePreviousMatched >= nextScorePreviousNonMatched)
                {
                    mScorePreviousMatched = nextScorePreviousMatched;
                    mConsecutiveMatchBonus = prevState.mConsecutiveMatchBonus + consecutiveMatchBonus;
                    return true;
                }
                else
                {
                    mScorePreviousMatched = nextScorePreviousNonMatched;
                    mConsecutiveMatchBonus = consecutiveMatchBonus;
                    return false;
                }
            }

            public JustScoreState NoMatch => cNoMatch;

            public int Score => Math.Max(mScorePreviousMatched, mScorePreviousNonMatched);

            public bool IsMatch => mScorePreviousNonMatched != int.MinValue;

            public bool IsMatchedHigherScoring => mScorePreviousMatched > mScorePreviousNonMatched;
        }

        private struct ScoreAndMatchPositionsState : IState<ScoreAndMatchPositionsState>
        {
            private JustScoreState mScoreState;
            private ImmutableArray<int>.Builder mMatchPositionsPreviousNonMatched;
            private ImmutableArray<int>.Builder mMatchPositionsPreviousMatched;

            private static readonly ScoreAndMatchPositionsState cNoMatch;

            static ScoreAndMatchPositionsState()
            {
                cNoMatch.mScoreState = default(JustScoreState).NoMatch;
            }

            public ScoreAndMatchPositionsState NoMatch => cNoMatch;

            public void Init(int stateIndex)
            {
                mScoreState.Init(stateIndex);
                mMatchPositionsPreviousNonMatched = ImmutableArray.CreateBuilder<int>(stateIndex);
                mMatchPositionsPreviousMatched = ImmutableArray.CreateBuilder<int>(stateIndex);
            }

            public bool UpdateMatch(ScoreAndMatchPositionsState prevState, int charMatchScore, int consecutiveMatchBonus, int charPosition)
            {
                Debug.Assert(mMatchPositionsPreviousMatched.Count == 0, "mMatchPositionsPreviousMatched.Count == 0");

                bool previousMatched = mScoreState.UpdateMatch(prevState.mScoreState, charMatchScore, consecutiveMatchBonus, charPosition);
                if (previousMatched)
                {
                    mMatchPositionsPreviousMatched.AddRange(prevState.mMatchPositionsPreviousMatched);
                }
                else
                {
                    mMatchPositionsPreviousMatched.AddRange(prevState.mMatchPositionsPreviousNonMatched);
                }

                mMatchPositionsPreviousMatched.Add(charPosition);
                return previousMatched;
            }

            public void UpdateAssumingNoMatch()
            {
                if (mScoreState.IsMatchedHigherScoring)
                {
                    var tmp = mMatchPositionsPreviousNonMatched;
                    mMatchPositionsPreviousNonMatched = mMatchPositionsPreviousMatched;
                    mMatchPositionsPreviousMatched = tmp;
                }

                mMatchPositionsPreviousMatched.Clear();

                mScoreState.UpdateAssumingNoMatch();
            }

            public bool IsMatch => mScoreState.IsMatch;

            public int Score => mScoreState.Score;

            public ImmutableArray<int> GetMatchPositions() => 
                mScoreState.IsMatchedHigherScoring
                    ? mMatchPositionsPreviousMatched.ToImmutable()
                    : mMatchPositionsPreviousNonMatched.ToImmutable();
        }

        public Scorer(string searchString, NormalizationForm normalizationForm = NormalizationForm.FormKD)
        {
            mNormalizationForm = normalizationForm;
            mSearchString = searchString.ToLowerInvariant().Normalize(mNormalizationForm);
            mJustScoreStates = new JustScoreState[mSearchString.Length + 1];
            mScoreAndMatchPositionsStates = new ScoreAndMatchPositionsState[mSearchString.Length + 1];
        }

        public int Score(string candidate)
        {
            JustScoreState lastState = RunScoringAlgorithm(mJustScoreStates, ref candidate);
            return lastState.IsMatch ? lastState.Score - candidate.Length : int.MinValue;
        }

        public ImmutableArray<int> GetMatchPositions(ref string candidate)
        {
            ScoreAndMatchPositionsState lastState = RunScoringAlgorithm(mScoreAndMatchPositionsStates, ref candidate);
            return lastState.IsMatch ? lastState.GetMatchPositions() : ImmutableArray<int>.Empty;
        }

        private TState RunScoringAlgorithm<TState>(TState[] states, ref string candidate)
            where TState : struct, IState<TState>
        {
            candidate = candidate.Normalize(mNormalizationForm);

            states[0].Init(0);
            int maxSearchCharsMatched = 0;

            char lastChar = default;
            int charPosition = 0;
            foreach (char c in candidate)
            {
                char lowerChar = char.ToLowerInvariant(c);
                for (int stateIndex = maxSearchCharsMatched; stateIndex >= 0; stateIndex--)
                {
                    ref TState state = ref states[stateIndex];
                    if (stateIndex >= mSearchString.Length)
                    {
                        state.UpdateAssumingNoMatch();
                        continue;
                    }

                    ref TState nextState = ref states[stateIndex + 1];
                    if (lowerChar == mSearchString[stateIndex])
                    {
                        // Score the match
                        int charMatchScore = 1;
                        int consecutiveMatchBonus = 1;

                        if (lastChar == default)
                        {
                            // Match at start of string
                            charMatchScore = 10;
                            consecutiveMatchBonus = 10;
                        }
                        else if (char.IsUpper(c) && !char.IsUpper(lastChar))
                        {
                            // Match at start of word (camelcase)
                            charMatchScore = 8;
                            consecutiveMatchBonus = 8;
                        }
                        else if (char.IsLetterOrDigit(c) && !char.IsLetterOrDigit(lastChar))
                        {
                            // Match at start of word (separator character)
                            charMatchScore = 8;
                            consecutiveMatchBonus = 8;
                        }

                        if (stateIndex == maxSearchCharsMatched)
                        {
                            nextState.Init(stateIndex);
                            maxSearchCharsMatched = stateIndex + 1;
                        }

                        nextState.UpdateMatch(state, charMatchScore, consecutiveMatchBonus, charPosition);
                    }

                    state.UpdateAssumingNoMatch();
                }

                lastChar = c;
                charPosition++;
            }

            if (maxSearchCharsMatched < mSearchString.Length)
            {
                return default(TState).NoMatch;
            }

            return states[maxSearchCharsMatched];
        }
    }
}
