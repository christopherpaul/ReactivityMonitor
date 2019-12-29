using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Utility
{
    public sealed class EnumerableComparer<T> : IComparer<IEnumerable<T>>
        where T : IComparable<T>
    {
        private readonly int mResultForXShorterThanY;

        public EnumerableComparer(bool longerBeforeShorter)
        {
            mResultForXShorterThanY = longerBeforeShorter ? 1 : -1;
        }

        public static EnumerableComparer<T> ShorterBeforeLonger { get; } = new EnumerableComparer<T>(false);
        public static EnumerableComparer<T> LongerBeforeShorter { get; } = new EnumerableComparer<T>(true);

        public int Compare(IEnumerable<T> x, IEnumerable<T> y)
        {
            using (var xIter = x.GetEnumerator())
            using (var yIter = y.GetEnumerator())
            {
                while (true)
                {
                    bool xHasNext = xIter.MoveNext();
                    bool yHasNext = yIter.MoveNext();

                    if (!xHasNext && !yHasNext)
                    {
                        return 0;
                    }

                    if (!xHasNext)
                    {
                        return mResultForXShorterThanY;
                    }

                    if (!yHasNext)
                    {
                        return -mResultForXShorterThanY;
                    }

                    int itemComparison = xIter.Current?.CompareTo(yIter.Current) ?? (yIter.Current == null ? 0 : -1);
                    if (itemComparison != 0)
                    {
                        return itemComparison;
                    }
                }
            }
        }
    }
}
