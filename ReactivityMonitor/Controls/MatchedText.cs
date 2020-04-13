using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ReactivityMonitor.Controls
{
    public static class MatchedText
    {
        public static readonly DependencyProperty PlainTextProperty = DependencyProperty.RegisterAttached(
            "PlainText",
            typeof(string),
            typeof(MatchedText),
            new FrameworkPropertyMetadata { PropertyChangedCallback = OnPropertyChanged });

        public static void SetPlainText(DependencyObject d, string plainText)
        {
            d.SetValue(PlainTextProperty, plainText);
        }

        public static string GetPlainText(DependencyObject d)
        {
            return (string)d.GetValue(PlainTextProperty);
        }

        public static readonly DependencyProperty MatchedCharPositionsProperty = DependencyProperty.RegisterAttached(
            "MatchedCharPositions",
            typeof(IEnumerable<int>),
            typeof(MatchedText),
            new FrameworkPropertyMetadata { PropertyChangedCallback = OnPropertyChanged });

        public static void SetMatchedCharPositions(DependencyObject d, IEnumerable<int> matchedCharPositions)
        {
            d.SetValue(MatchedCharPositionsProperty, matchedCharPositions);
        }

        public static IEnumerable<int> GetMatchedCharPositions(DependencyObject d)
        {
            return (IEnumerable<int>)d.GetValue(MatchedCharPositionsProperty);
        }

        public static readonly DependencyProperty MatchedCharStyle = DependencyProperty.RegisterAttached(
            "MatchedCharStyle",
            typeof(Style),
            typeof(MatchedText),
            new FrameworkPropertyMetadata { PropertyChangedCallback = OnPropertyChanged });

        public static void SetMatchedCharStyle(DependencyObject d, Style style)
        {
            d.SetValue(MatchedCharStyle, style);
        }

        public static Style GetMatchedCharStyle(DependencyObject d)
        {
            return (Style)d.GetValue(MatchedCharStyle);
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            InlineCollection inlines = GetInlines(d);
            if (inlines == null)
            {
                return;
            }

            inlines.Clear();

            string plainText = GetPlainText(d) ?? string.Empty;
            IEnumerable<int> matchedCharPositions = GetMatchedCharPositions(d) ?? Enumerable.Empty<int>();
            Style matchedCharStyle = GetMatchedCharStyle(d);

            int nextPos = 0;
            int matchRunStart = 0;
            foreach (int matchPos in matchedCharPositions.OrderBy(p => p).Where(p => p < plainText.Length))
            {
                if (matchPos != nextPos)
                {
                    AddMatchRun();

                    var nonMatchRun = new Run(plainText.Substring(nextPos, matchPos - nextPos));
                    inlines.Add(nonMatchRun);

                    matchRunStart = matchPos;
                }

                nextPos = matchPos + 1;
            }

            AddMatchRun();

            if (nextPos < plainText.Length)
            {
                var nonMatchRun = new Run(plainText.Substring(nextPos));
                inlines.Add(nonMatchRun);
            }

            void AddMatchRun()
            {
                if (matchRunStart < nextPos)
                {
                    var matchRun = new Run(plainText.Substring(matchRunStart, nextPos - matchRunStart))
                    {
                        Style = matchedCharStyle
                    };
                    inlines.Add(matchRun);
                }
            }
        }

        private static InlineCollection GetInlines(DependencyObject d)
        {
            if (d is TextBlock textBlock)
            {
                return textBlock.Inlines;
            }

            if (d is Span span)
            {
                return span.Inlines;
            }

            return null;
        }
    }
}
