using Codex.ObjectModel;
using Codex.ObjectModel.Implementation;
using Codex.Sdk.Search;
using Codex.Utilities;
using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Web;
using Range = Codex.Utilities.Extent;

namespace Codex.View
{
    public static partial class ViewUtilities
    {
        //public static Visibility BoolVisibility(bool isVisible)
        //{
        //    return isVisible ? Visibility.Visible : Visibility.Collapsed;
        //}

        public static string GetOverviewFileName(this OverviewKind mode)
        {
            return mode == OverviewKind.help ? "overview.html" : $"{mode}.html";
        }

        public static void Bind<TValue>(this Bound<TValue> bound, Action<TValue> onUpdate)
        {
            bound.OnUpdate(value => onUpdate(value));
            onUpdate(bound.Value);
        }

        public static T Increment<T>(this T value, Counter counter)
        {
            counter.Increment();
            return value;
        }

        public static T Add<T>(this T value, Func<T, int> computeCount)
            where T : IResultsStats
        {
            var count = computeCount(value);
            value.Counter.Add(count);
            return value;
        }

        public static T AddTo<T>(this T value, Counter counter)
            where T : IResultsStats
        {
            counter.Add(value.Counter.Count);
            return value;
        }

        public static T AddFrom<T>(this T value, Counter counter)
            where T : IResultsStats
        {
            value.Counter.Add(counter.Count);
            return value;
        }

        public static EncodedString Url(string value)
        {
            return HttpUtility.UrlEncode(value).AsEncoded();
        }

        public static EncodedString Html(StringSpan value)
        {
            return HttpUtility.HtmlEncode(value.StringValue).AsEncoded();
        }

        public static string HtmlEncode(object value)
        {
            if (value is EncodedString encoded)
            {
                return encoded.Value;
            }

            return HttpUtility.HtmlEncode(value);
        }

        public static void Html(StringSpan value, TextWriter writer)
        {
            HttpUtility.HtmlEncode(value.StringValue, writer);
        }

        public static EncodedString Attr(string value)
        {
            return HttpUtility.HtmlAttributeEncode(value).AsEncoded();
        }

        public static EncodedString AsEncoded(this string value)
        {
            return new EncodedString(value);
        }

        public record struct EncodedString(string Value)
        {
            public static implicit operator string(EncodedString value)
            {
                return value.Value;
            }

            public override string ToString()
            {
                return Value;
            }
        }

        public static IComparer<IReferenceSearchResult> LeftPaneReferencesSorter = new ComparerBuilder<IReferenceSearchResult>()
            .CompareByAfter(r => r.ReferenceSpan.Reference.ReferenceKind.GetPreference())
            .CompareByAfter(r => r.File.ProjectId)
            .CompareByAfter(r => r.File.ProjectRelativePath)
            .CompareByAfter(r => r.ReferenceSpan.LineNumber);

        private static IUnifiedComparer<TextSpanSearchResult> TextSpanSearchResultComparer = new ComparerBuilder<TextSpanSearchResult>()
            .CompareByAfter(t => t.SearchResult.ProjectId)
            .CompareByAfter(t => t.SearchResult.ProjectRelativePath)
            .CompareByAfter(t => t.Span.LineNumber)
            .CompareByAfter(t => t.Span.LineSpanText);

        public static IEnumerable<(TextSpanSearchResult result, IEnumerable<RichText> spans)> MergeResults(IEnumerable<TextSpanSearchResult> results)
        {
            return results.OrderBy(t => t.Span.Start).ThenBy(t => t.Span.Length).GroupBy(t => t, TextSpanSearchResultComparer).Select(g =>
                (g.Key, MergeSpans(g.Key, g.Select(s => s.Span))));
        }

        public static IEnumerable<TextSpanSearchResultViewModel> ToMergedViews(IEnumerable<TextSpanSearchResult> results)
        {
            var maxLineNumber = results.Max(t => t.Span.LineNumber).ToString();
            return results.OrderBy(t => t.Span.LineNumber).ThenBy(t => t.Span.Start).ThenBy(t => t.Span.LineSpanStart).ThenBy(t => t.Span.Length).GroupBy(t => t, TextSpanSearchResultComparer).Select(g =>
                new TextSpanSearchResultViewModel(g.Key, MergeSpans(g.Key, g.Select(s => s.Span)), maxLineNumber.Length) { ReferenceCount = g.Count() });
        }

        private static IEnumerable<RichText> MergeSpans(TextSpanSearchResult key, IEnumerable<ILineSpan> spans)
        {
            int cursor = 0;
            CharString fullText = key.Span.LineSpanText;
            foreach (var span in spans)
            {
                if (cursor < span.LineSpanStart)
                {
                    // Emit plain text for portion prior to highlighted span
                    yield return new RichText(fullText.AsMemory(cursor, span.LineSpanStart - cursor));
                    cursor = span.LineSpanStart;
                }
                
                if (cursor >= span.LineSpanStart)
                {
                    if (cursor < span.LineSpanEnd())
                    {
                        // Emit highlighted portion of text
                        yield return new RichText(fullText.AsMemory(cursor, span.LineSpanEnd() - cursor), Highlighted: true);
                        cursor = span.LineSpanEnd();
                    }
                }
            }

            if (cursor < fullText.Length)
            {
                // Emit remaining text
                yield return new RichText(fullText.AsMemory(cursor, fullText.Length - cursor));
            }
        }

        public static string GetReferencesHeader(ReferenceKind referenceKind, int referenceCount, string symbolName)
        {
            string formatString = "";
            switch (referenceKind)
            {
                default:
                case ReferenceKind.Reference:
                    formatString = "{0} reference{1} to {2}";
                    break;
                case ReferenceKind.ExplicitCast:
                    formatString = "{0} explicit cast{1} to {2}";
                    break;
                case ReferenceKind.Definition:
                    formatString = "{0} definition{1} of {2}";
                    break;
                case ReferenceKind.TypeForwardedTo:
                    formatString = "{0} type forward{1} of {2}";
                    break;
                case ReferenceKind.Constructor:
                    formatString = "{0} constructor{1} of {2}";
                    break;
                case ReferenceKind.Instantiation:
                    formatString = "{0} instantiation{1} of {2}";
                    break;
                case ReferenceKind.CopyWith:
                    formatString = "{0} modified clone{1} of {2}";
                    break;
                case ReferenceKind.DerivedType:
                    formatString = "{0} type{1} derived from {2}";
                    break;
                case ReferenceKind.InterfaceInheritance:
                    formatString = "{0} interface{1} inheriting from {2}";
                    break;
                case ReferenceKind.InterfaceImplementation:
                    formatString = "{0} implementation{1} of {2}";
                    break;
                case ReferenceKind.Override:
                    formatString = "{0} override{1} of {2}";
                    break;
                case ReferenceKind.InterfaceMemberImplementation:
                    formatString = "{0} implementation{1} of {2}";
                    break;
                case ReferenceKind.Write:
                    formatString = "{0} write{1} to {2}";
                    break;
                case ReferenceKind.Read:
                    formatString = "{0} read{1} of {2}";
                    break;
                case ReferenceKind.MSBuildUsage:
                    formatString = "{0} usage{1} of {2}";
                    break;
                case ReferenceKind.GuidUsage:
                    formatString = "{0} usage{1} of Guid {2}";
                    break;
                case ReferenceKind.EmptyArrayAllocation:
                    formatString = "{0} allocation{1} of empty arrays";
                    break;
                case ReferenceKind.Text:
                    formatString = "{0} text search hit{1} for '{2}'";
                    break;
                //default:
                    ///throw new NotImplementedException("Missing case for " + referenceKind);
            }

            return string.Format(formatString,
                    referenceCount,
                    referenceCount == 1 ? "" : "s",
                    symbolName);
        }
    }
}
