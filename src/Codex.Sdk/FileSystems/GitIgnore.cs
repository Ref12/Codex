using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotNext.Collections.Generic;

namespace Codex
{
    public record GitIgnore(string ExclusionPattern, string InclusionPattern, RegexOptions RegexOptions, bool invert, int PatternCount, List<string> Lines)
    {
        public static bool Debug = false;
        //public static bool Debug = System.Diagnostics.Debugger.IsAttached;

        public RegexOptions RegexOptions { get; } = RegexOptions | RegexOptions.ExplicitCapture;

        private Regex m_inclusionRegex;
        private Regex m_exclusionRegex;

        public Regex InclusionRegex
        {
            get
            {
                if (m_inclusionRegex == null)
                {
                    m_inclusionRegex = new Regex(InclusionPattern, RegexOptions);
                }

                return m_inclusionRegex;
            }
        }

        public Regex ExclusionRegex
        {
            get
            {
                if (m_exclusionRegex == null)
                {
                    m_exclusionRegex = new Regex(ExclusionPattern, RegexOptions);
                }

                return m_exclusionRegex;
            }
        }

        public bool Includes(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            int prefixLength = PatternCount;
            var length = path.Length + prefixLength;
            var inputSpan = Perf.TryAllocateIfLarge<char>(length) ?? stackalloc char[length];

            var pathSpan = path.AsSpan();
            pathSpan.CopyTo(inputSpan.Slice(prefixLength));
            inputSpan.Replace('\\', '/');

            if (inputSpan[prefixLength] == '/')
            {
                inputSpan = inputSpan.Slice(1);
            }

            inputSpan.Slice(0, prefixLength).Fill('|');


            var excludes = ExclusionRegex.Match(inputSpan);
            var includes = InclusionRegex.Match(inputSpan);

            if (Debug)
            {
                var input = inputSpan.ToString();
                var ematch = ExclusionRegex.Match(input);
                var imatch = InclusionRegex.Match(input);

                var egmatch = ematch.Groups.AsEnumerable<Group>().Skip(1).FirstOrDefault(g => g.Success);
                var igmatch = imatch.Groups.AsEnumerable<Group>().Skip(1).FirstOrDefault(g => g.Success);

                if (input.EndsWith("/.gitignore"))
                {

                }
            }

            if (excludes.Success && includes.Success)
            {
                return includes.Match.Index < excludes.Match.Index;
            }

            if (invert)
            {
                return includes.Success;
            }
            else
            {
                return !excludes.Success;
            }
        }

        public bool Excludes(string path)
        {
            return !Includes(path);
        }

        public static GitIgnore Parse(IReadOnlyList<string> lines, bool tfIgnore = false, bool invert = false, RegexOptions regexOptions = default)
        {
            return Parse(new CompositeTextReader(lines.SelectManyList(2, t => Requires.Expect<CharString>(t.SubIndex == 0 ? "\n" : t.Item))), tfIgnore: tfIgnore, invert: invert, regexOptions);
        }

        public static GitIgnore Parse(TextReader reader, bool tfIgnore = false, bool invert = false, RegexOptions regexOptions = default)
        {
            List<string> negatives = new List<string>();
            List<string> positives = new List<string>();

            List<string> lines = new List<string>();

            string line = null;
            int index = 0;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var isNegative = line[0] == '!';

                if (isNegative)
                {
                    line = line.Substring(1);
                }

                isNegative ^= invert;
                if (line.Length == 0)
                {
                    continue;
                }

                lines.Add(line);
                var list = isNegative ? negatives : positives;
                list.Add(PrepareRegexPattern(line, tfIgnore, ++index));
            }

            return new GitIgnore(Combine(positives), Combine(negatives), regexOptions, invert, index + 1, lines);
        }

        public static GitIgnore Parse(string filePath, bool tfIgnore = false, bool invert = false, RegexOptions regexOptions = default)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                return Parse(reader, tfIgnore, invert, regexOptions);
            }
        }

        private static string Combine(List<string> expressions)
        {
            if (expressions.Count == 0)
            {
                return "$^";
            }
            else
            {
                return string.Join("|", expressions);
            }
        }

        private static readonly string RegexEscapeCharsRegex = $"[{Regex.Escape(@"-.()+^$|{}\ ")}]";

        private record struct PlaceholderChar(ushort CharValue, string Value)
        {
            public string CharString { get; } = $"${(char)CharValue}";

            public string Mark(string value) => value.Replace(Value, CharString);
            public string Unmark(string value) => value.Replace(CharString, Value);
        }

        private static readonly PlaceholderChar Star = new PlaceholderChar(1, "*");
        private static readonly PlaceholderChar StarStarSlash = new PlaceholderChar(2, "**/");
        private static readonly PlaceholderChar QuestionMark = new PlaceholderChar(3, "?");

        private static string PrepareRegexPattern(string line, bool tfIgnore, int index)
        {
            if (tfIgnore)
            {
                line = line.Replace("\\", "/");
            }

            bool prefixMatch = false;
            if (line[0] == '/')
            {
                line = line.Substring(1);
                prefixMatch = true;
            }
            else if (!tfIgnore && line.StartsWith("**/"))
            {
                line = line.Substring(3);
            }

            bool matchFileOrDirectory = line[line.Length - 1] != '/';

            line = Regex.Replace(line, RegexEscapeCharsRegex, "\\$0");

            if (tfIgnore)
            {
                line = QuestionMark.Mark(line);
                line = Star.Mark(line);

                line = line.Replace(QuestionMark.CharString, "[^\\|]?");
                line = line.Replace(Star.CharString, "([^\\|]+)");
            } 
            else
            {
                line = QuestionMark.Mark(line);
                line = StarStarSlash.Mark(line);
                line = Star.Mark(line);

                line = line.Replace(QuestionMark.CharString, "[^\\|]");
                line = line.Replace(StarStarSlash.CharString, "([^\\|/]+/)*");
                line = line.Replace(Star.CharString, "(([^\\|/]*)|$)");
            }

            line = QuestionMark.Unmark(line);
            line = StarStarSlash.Unmark(line);
            line = Star.Unmark(line);

            if (!prefixMatch)
            {
                line = "([^\\|]*\\/)?" + line;
            }

            if (matchFileOrDirectory)
            {
                line += "(/|$)";
            }

            return $@"({GetCaptureExpression(index)}\|{{{index}}}{line})";
        }

        private static string GetCaptureExpression(int index)
        {
            return Debug ? $"?<n{index}>" : "";
        }
    }
}
