using System.Text.RegularExpressions;

namespace Codex.Search
{
    public static class SearchUtilities
    {
        private static readonly Regex GenericTypeArgumentsRegex = new Regex(@"<[\w\s,]*>?");
        private static readonly char[] QualifiedNameSeparators = new char[] { '.', '/', '\\' };

        public static IEnumerable<string> EnumerateContainerQualifiedNameFieldValues(string value)
        {
            foreach (var item in EnumerateContainerQualifiedNameUnhashedFieldValues(value))
            {
                string result = GetHashedQualifiedNameValue(item);
                yield return result;
            }
        }

        public static string GetHashedQualifiedNameValue(string item)
        {
            var typeArgSpecifierIndex = item.IndexOf('`');
            ReadOnlySpan<char> hashedPortion = item;
            var suffix = string.Empty;
            if (typeArgSpecifierIndex > 0)
            {
                hashedPortion = hashedPortion.Slice(0, typeArgSpecifierIndex);
                suffix = item.Substring(typeArgSpecifierIndex);
            }

            var result = IndexingUtilities.ComputeSymbolUid(hashedPortion) + suffix;
            return result;
        }

        public static IEnumerable<string> EnumerateContainerQualifiedNameUnhashedFieldValues(string value)
        {
            value = GetNameTransformedValue(value);
            while (!string.IsNullOrEmpty(value))
            {
                yield return value;

                var index = value.IndexOfAny(QualifiedNameSeparators);
                if (index > 0)
                {
                    value = value.Substring(index + 1);
                }
                else
                {
                    break;
                }
            }
        }

        public static string GetNameTransformedValue(string value, bool lowercase = true)
        {
            if (lowercase)
            {
                value = value.ToLowerInvariant();
            }

            if (!value.Contains('<'))
            {
                // Early out for values which don't contain generics
                return value;
            }

            return GenericTypeArgumentsRegex.Replace(value, match =>
            {
                if (match.Index + match.Length == value.Length)
                {
                    if (match.ValueSpan[^1] != '>')
                    {
                        return "`";
                    }

                    var argumentCount = match.ValueSpan.Count(',') + 1;
                    return "`" + argumentCount;
                }
                else
                {
                    return string.Empty;
                }
            });
        }

        public static QualifiedNameTerms CreateNameTerm(this string nameTerm)
        {
            var terms = new QualifiedNameTerms();
            PopulateNameTerms(terms, nameTerm);
            return terms;
        }

        private static void PopulateNameTerms(QualifiedNameTerms terms, string nameTerm)
        {
            string secondaryNameTerm = string.Empty;
            if (!string.IsNullOrEmpty(nameTerm))
            {
                nameTerm = nameTerm.Trim();
                nameTerm = nameTerm.TrimStart('"');
                if (!string.IsNullOrEmpty(nameTerm))
                {
                    terms.RawNameTerm = nameTerm;

                    if (nameTerm.EndsWith("\""))
                    {
                        nameTerm = nameTerm.TrimEnd('"');
                        nameTerm += "$";
                    }

                    if (!string.IsNullOrEmpty(nameTerm))
                    {
                        if (nameTerm[0] == '*')
                        {
                            nameTerm = nameTerm.TrimStart('*');
                            secondaryNameTerm = nameTerm.Trim();
                            nameTerm = "^" + secondaryNameTerm;
                        }
                        else
                        {
                            nameTerm = "^" + nameTerm;
                        }
                    }
                }
            }

            terms.NameTerm = nameTerm;
            terms.SecondaryNameTerm = secondaryNameTerm;
        }

        public static QualifiedNameTerms ParseContainerAndName(string fullyQualifiedTerm)
        {
            QualifiedNameTerms terms = new QualifiedNameTerms();
            int indexOfLastSeparator = fullyQualifiedTerm.LastIndexOfAny(QualifiedNameSeparators);
            if (indexOfLastSeparator >= 0)
            {
                terms.ContainerTerm = fullyQualifiedTerm.Substring(0, indexOfLastSeparator);
            }

            terms.NameTerm = fullyQualifiedTerm.Substring(indexOfLastSeparator + 1);

            if (terms.NameTerm.Length > 0)
            {
                PopulateNameTerms(terms, terms.NameTerm);
            }
            return terms;
        }
    }
}
