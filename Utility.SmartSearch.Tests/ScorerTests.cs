using NUnit.Framework;
using Utility.SmartSearch;

namespace Utility.SmartSearch.Tests
{
    [TestFixture]
    public class ScorerTests
    {
        [TestCase("abc", "abc")]
        [TestCase("abc", "Abc")]
        [TestCase("abc", "anybeercold")]
        [TestCase("aaa", "alphabeta")]
        public void TestMatches(string searchFor, string candidate)
        {
            var scorer = new Scorer(searchFor);
            Assert.That(scorer.Score(candidate), Is.Not.EqualTo(int.MinValue));
        }

        [TestCase("abc", "xyzzy")]
        [TestCase("abc", "abxyzzy")]
        [TestCase("abc", "bc")]
        [TestCase("abc", "cab")]
        [TestCase("aaa", "alpha")]
        public void TestNonMatches(string searchFor, string candidate)
        {
            var scorer = new Scorer(searchFor);
            Assert.That(scorer.Score(candidate), Is.EqualTo(int.MinValue));
        }

        [TestCase("abc", "abc", "axbc")]
        [TestCase("abcd", "abcxd", "abxcd")]
        [TestCase("score", "scorertests", "screenbore")]
        [TestCase("byt", "byte", "multibyte")]
        [TestCase("byt", "BigYellowTaxi", "BuyBritish")]
        [TestCase("byt", "A_BIG_YELLOW_TAXI", "abigyellowtaxi")]
        [TestCase("byt", "a_big_yellow_taxi", "abigyellowtaxi")]
        [TestCase("byte", "MultiByte", "BigYellowTaxidermist")]
        [TestCase("ulti", "theUltimate", "MultiByte")]
        public void TestMatchRelativeScoring(string searchFor, string betterCandidate, string worseCandidate)
        {
            var scorer = new Scorer(searchFor);
            Assert.That(scorer.Score(betterCandidate), Is.GreaterThan(scorer.Score(worseCandidate)));
        }

        [TestCase("abc", "abc", new[] { 0, 1, 2 })]
        [TestCase("abcd", "abcxd", new[] { 0, 1, 2, 4 })]
        [TestCase("abc", "bcabbacd", new[] { 2, 3, 6 })]
        [TestCase("abc", "abcAbc", new[] { 0, 1, 2 })]
        [TestCase("abc", "DabcAbc", new[] { 4, 5, 6 })]
        [TestCase("abc", "acmeBicycleChain", new[] { 0, 4, 11 })]
        [TestCase("abc", "ACME_BICYCLE_CHAIN", new[] { 0, 5, 13 })]
        public void TestMatchPositions(string searchFor, string candidate, int[] expectedPositions)
        {
            var scorer = new Scorer(searchFor);
            Assert.That(scorer.GetMatchPositions(ref candidate), Is.EqualTo(expectedPositions));
        }
    }
}