using Codex.Utilities;

namespace Codex.Web.Common
{
    public static class SearchResultSorter
    {
        public static IEnumerable<IDefinitionSymbol> OrderByRelevance(this IEnumerable<IDefinitionSymbol> entries, string searchTerm)
        {
            var queryInfos = QueryRelevanceInfo.Create(searchTerm);
            return entries.Distinct<IDefinitionSymbol>(CodeSymbol.SymbolEqualityComparer).Select(e => new SymbolRelevanceInfo(e)
            {
                MatchLevel = queryInfos.Sum(queryInfo => MatchLevel(e, queryInfo)),
                KindRank = Rank(e)
            })
            .OrderBy(i => i, Comparer.Instance)
            .Select(e => e.Symbol);
        }

        public record struct SymbolRelevanceInfo(IDefinitionSymbol Symbol)
        {
            public int MatchLevel { get; set; }
            public int KindRank { get; set; }
            public string DisplayName => Symbol.DisplayName ?? Symbol.ShortName;
        }

        public record struct QueryRelevanceInfo(string Value, bool IsAllLowercase)
        {
            public static QueryRelevanceInfo[] Create(string value)
            {
                return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim(' ', '"', '^', '$'))
                    .Select(v => new QueryRelevanceInfo(v, IsAllLowercase: v == v.ToLowerInvariant())).ToArray();
            }
        }

        public class Comparer : IComparer<SymbolRelevanceInfo>
        {
            public static readonly Comparer Instance = new Comparer();

            public int Compare(SymbolRelevanceInfo x, SymbolRelevanceInfo y)
            {
                return SymbolSorter(x, y);
            }
        }

        public static readonly Comparer SymbolComparer = new Comparer();

        private static readonly IComparer<SymbolRelevanceInfo> SymbolRelevance = new ComparerBuilder<SymbolRelevanceInfo>()
            .CompareByAfter(c => c.MatchLevel)
            .CompareByAfter(c => c.KindRank)
            .CompareByAfter(c => GetProjectRank(c.Symbol.ProjectId))
            ;

        private static int GetProjectRank(string projectId)
        {
            // Special case BCL projects
            if (projectId.StartsWith("System.") || projectId == "System" || projectId == "mscorlib")
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        /// <summary>
        /// This defines the ordering of results based on the kind of symbol and other heuristics
        /// </summary>
        public static int SymbolSorter(SymbolRelevanceInfo left, SymbolRelevanceInfo right)
        {
            if (left == right)
            {
                return 0;
            }

            var comparison = SymbolRelevance.Compare(left, right);
            if (comparison != 0)
            {
                return comparison;
            }

            if (left.Symbol.ShortName != null && right.Symbol.ShortName != null)
            {
                comparison = left.Symbol.ShortName.CompareTo(right.Symbol.ShortName);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            comparison = left.Symbol.ProjectId.CompareTo(right.Symbol.ProjectId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
            return comparison;
        }

        public static int MatchLevel(IDefinitionSymbol symbol, QueryRelevanceInfo queryInfo)
        {
            int value = int.MaxValue;

            void check(ReadOnlySpan<char> candidate, int multiplier = 1)
            {
                value = Math.Min(MatchLevel(candidate, queryInfo) * multiplier, value);
            }

            string shortName = symbol.ShortName ?? "";
            check(shortName);

            // Check short name without generic arguments
            if (shortName.Contains("<"))
            {
                check(shortName.AsSpan().SubstringBeforeFirstIndexOfAny("<"));
            }

            // Check short name without generic arguments
            if (shortName.Contains('`'))
            {
                check(shortName.AsSpan().SubstringBeforeFirstIndexOfAny("`"));
            }

            check(symbol.AbbreviatedName, 2);
            check(symbol.ProjectId ?? "", multiplier: Rank(SymbolKinds.Project));
            check(symbol.Kind.StringValue ?? "", Rank(symbol));

            return value;
        }

        /// <summary>
        /// This defines the ordering of the results, assigning weight to different types of matches
        /// </summary>
        public static ushort MatchLevel(ReadOnlySpan<char> candidate, QueryRelevanceInfo queryInfo)
        {
            var query = queryInfo.Value;
            int indexOfIgnoreCase = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);

            // When all lower, we assume case isn't relevant
            int indexOf = queryInfo.IsAllLowercase ? indexOfIgnoreCase : candidate.IndexOf(query);

            if (indexOf == 0)
            {
                if (candidate.Length == query.Length)
                {
                    // candidate == query
                    return 1;
                }
                else
                {
                    // candidate.StartsWith(query)
                    return 3;
                }
            }
            else if (indexOf > 0)
            {
                if (indexOfIgnoreCase == 0)
                {
                    if (candidate.Length == query.Length)
                    {
                        // candidate.Contains(query) && candidate.EqualsIgnoreCase(query)
                        return 2;
                    }
                    else
                    {
                        // candidate.Contains(query) && candidate.StartsWithIgnoreCase(query)
                        return 4;
                    }
                }
                else
                {
                    // candidate.Contains(query)
                    return 5;
                }
            }
            else // indexOf < 0
            {
                if (indexOfIgnoreCase == 0)
                {
                    if (candidate.Length == query.Length)
                    {
                        // query.EqualsIgnoreCase(candidate)
                        return 2;
                    }
                    else
                    {
                        // candidate.StartsWithIgnoreCase(query)
                        return 4;
                    }
                }
                else if (indexOfIgnoreCase > 0)
                {
                    // candidate.ContainsIgnoreCase(query)
                    return 7;
                }
                else
                {
                    // !candidate.ContainsIgnoreCase(query)
                    return 8;
                }
            }
        }

        public static int Rank(IDefinitionSymbol symbol)
        {
            var kind = symbol.Kind.Value ?? SymbolKinds.Unknown;
            return Rank(kind);
        }

        private static int Rank(SymbolKinds kind)
        {
            return getRank() + 10;
            ushort getRank()
            {
                switch (kind)
                {
                    case SymbolKinds.Class:
                    case SymbolKinds.Struct:
                    case SymbolKinds.Interface:
                    case SymbolKinds.Enum:
                    case SymbolKinds.Delegate:
                        return 1;
                    case SymbolKinds.Field:
                    case SymbolKinds.Property:
                    case SymbolKinds.Method:
                        return 3;
                    case SymbolKinds.File:
                        return 4;
                    default:
                        return 2;
                }
            }
        }
    }
}
