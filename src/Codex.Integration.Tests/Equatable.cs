using System.Collections.Immutable;
using Codex.ObjectModel;
using Codex.ObjectModel.Implementation;
using Codex.Sdk;
using Codex.Sdk.Utilities;
using Codex.Utilities;

namespace Codex.Integration.Tests;

public static class Equatable
{
    public static Equatable<T> Equate<T>(T actual, T expected)
    {
        return new(actual, expected);
    }

    public static void SequenceEqual<T, TItem>(this Equatable<T> eq, Func<T, IEnumerable<TItem>> select, Action<Equatable<TItem>, int> equate)
    {
        var items = eq.Select(select);
        items.Actual.SequenceEqual(items.Expected, equate);
    }

    public static void SequenceEqual<T>(this Equatable<IEnumerable<T>> eq, Action<Equatable<T>, int> equate)
    {
        eq.Actual.SequenceEqual(eq.Expected, equate);
    }

    public static void SequenceEqual<T>(this IEnumerable<T> actual, IEnumerable<T> expected, Action<Equatable<T>, int> equate)
    {
        CollectionUtilities.DistinctMergeSorted(
            actual.WithIndices(),
            expected.WithIndices(),
            i => i.Index,
            i => i.Index)
            .Count(r =>
            {
                r.mode.Should().Be(CollectionUtilities.MergeMode.Both,
                    "Unexpected item in {0} collection at index {1}",
                    r.mode,
                    r.Either().Index);

                equate(Equate(r.left.Item, r.right.Item), r.left.Index);
                return true;
            });
    }

    public static Equatable<TValue>? SelectNullable<T, TValue>(this Equatable<T?> e, Func<T, TValue> select)
        where T : struct
    {
        e.Actual.HasValue.Should().Be(e.Expected.HasValue);

        return e.Actual.HasValue
            ? e.Select(s => select(s.Value))
            : default;
    }

    public static Equatable<TValue>? SelectNullable<T, TValue>(this Equatable<T> e, Func<T, TValue> select)
        where T : class
    {
        (e.Actual == null).Should().Be(e.Expected == null);

        return e.Actual != null
            ? e.Select(s => select(s))
            : default;
    }

    public static Equatable<TValue> Equate<T, TValue>(T actual, T expected, Func<T, TValue> getValue)
    {
        return new(getValue(actual), getValue(expected));
    }

    public static void DefinitionSpanEquals(this Equatable<IDefinitionSpan> equatable, bool includePosition = false)
    {
        equatable.Select(rs => rs.Definition).DefinitionEquals();
        equatable.Select(rs =>
        {
            return
            new
            {
                Start = includePosition ? rs.Start : 0,
                rs.Length,
            };
        }).AssertEquivalent();
    }

    public static void DefinitionEquals(this Equatable<IDefinitionSymbol> equatable)
    {
        equatable.Select(ds => ds.As<IReferenceSymbol>()).ReferenceEquals();
        equatable.Select(ds =>
        {
            return
            new
            {
                ds.DisplayName,
            };
        }).AssertEquivalent();
    }

    public static void ReferenceEquals(this Equatable<IReferenceSymbol> equatable)
    {
        equatable.Select(rs =>
        {
            return
            new
            {
                rs.Kind,
                rs.ReferenceKind,
                rs.ExcludeFromSearch,
                Id = rs.Id.Value,
                rs.ProjectId,

            };
        }).AssertEquivalent();
    }

    public static void LocationEquals(this Equatable<IProjectFileScopeEntity> equatable)
    {
        equatable.Select(f =>
        {
            return new
            {
                f.ProjectId,
                f.ProjectRelativePath,
                f.RepoRelativePath,
                f.RepositoryName,
            };
        }).AssertEquivalent();
    }

    public static void SymbolEquals<T>(this Equatable<T> equatable)
        where T : ICodeSymbol
    {
        equatable.Select(f =>
        {
            return new
            {
                f.ProjectId,
                Id = f.Id.Value,
                Kind = f.Kind.Value
            };
        }).AssertEquivalent();

    }

    public static void FileEquals(this Equatable<IBoundSourceFile> equatable,
        bool includeSourceSpans = true,
        bool compareLocalIds = true,
        bool includeContent = false,
        bool expectPostProcessed = false)
    {
        equatable.Select(b => b.SourceFile).Select(f =>
        {
            return new
            {
                Content = includeContent ? f.Content : null,
                f.Info.ProjectId,
                f.Info.ProjectRelativePath,
                f.Info.RepoRelativePath,
                f.Info.RepositoryName,
                f.Flags
            };
        }).AssertEquivalent();

        void referenceSpanEquals(Equatable<IReferenceSpan> equatable)
        {
            equatable.ReferenceSpanEquals(
                includeContainer: expectPostProcessed,
                includeRelatedDefinition: expectPostProcessed);
        }

        equatable.SequenceEqual(b => b.References, (equatable, index) =>
        {
            referenceSpanEquals(equatable);
        });

        if (includeSourceSpans)
        {
            equatable.Select(b => (BoundSourceFile)b).Select(
                b => b.SourceSpans,//.ApplyIf(!expectPostProcessed, s => s.Rejoin(b.SourceFile.Content)), 
                b => b.SourceSpans ?? new SourceFileModel(b).GetProcessedSpans(trim: true))
            .Assert(equatable =>
            {
                equatable.SequenceEqual((equatable, index) =>
                {
                    equatable.Select(v => v.AsExtent()).Assert(e => e.Actual.Should().BeEquivalentTo(e.Expected));

                    equatable.Select(v => v.Classification?.Classification).AssertEquivalent();

                    if (compareLocalIds)
                    {
                        equatable.Select(v => v.Classification?.LocalGroupId).AssertEquivalent();
                    }

                    var count = equatable.Select(v => v.SpanReferences?.Count ?? 0).Assert(e => e.Actual.Should().Be(e.Expected));
                    if (count.Actual == 0) return;

                    //if (equatable.Actual.SpanReferences)

                    equatable.Select(v => v.SpanReferences).SelectNullable(i => i)?.Assert(e =>
                    {
                        e.Actual.SequenceEqual(e.Expected, (equatable, index) =>
                        {
                            referenceSpanEquals(equatable);
                        });
                    });

                    referenceSpanEquals(equatable.Select(v => v.Reference));
                });
            });
        }

        equatable.SequenceEqual(b => b.Definitions, (equatable, index) =>
        {
            equatable.DefinitionSpanEquals(includePosition: true);
        });
    }

    public static void ReferenceSpanEquals(this Equatable<IReferenceSpan> equatable,
        bool includeText = false,
        bool includeStart = true,
        bool includeContainer = true,
        bool includeRelatedDefinition = true)
    {
        equatable.Select(rs => rs.Reference).ReferenceEquals();

        equatable.Select(rs =>
        {
            int offset = 0;
            CharString lineSpanText = default;
            if (includeText && (lineSpanText = rs.LineSpanText).Length != 0)
            {
                var startTrimmed = lineSpanText.Chars.TrimStart();
                offset = lineSpanText.Length - startTrimmed.Length;
                lineSpanText = lineSpanText.Chars.Trim();
            }

            return new
            {
                Start = includeStart ? rs.Start : 0,
                rs.Length,
                rs.LineNumber,
                rs.LineOffset,
                IsImplicitlyDeclared = !includeContainer ? default : rs.IsImplicitlyDeclared,
                LineSpanStart = includeText ? rs.LineSpanStart - offset : 0,
                ContainerId = !includeContainer ? default : rs.ContainerSymbol?.Id,
                ContainerProjectId = !includeContainer ? default : rs.ContainerSymbol?.ProjectId,
                RelatedDefintion = !includeRelatedDefinition ? default : rs.RelatedDefinition,
                lineSpanText
            };
        }).AssertEquivalent();
    }

}

public record struct Equatable<T>(T Actual, T Expected)
{
    public void AssertEquivalent()
    {
        Actual.Should().BeEquivalentTo(Expected, o => o.ComparingRecordsByValue());
    }

    public Equatable<T> Assert(Action<Equatable<T>> assert)
    {
        assert(this);
        return this;
    }

    public Equatable<TValue> Select<TValue>(Func<T, TValue> select, Func<T, TValue> selectExpected = default)
    {
        selectExpected ??= select;
        TValue checkedSelect(T source, Func<T, TValue> select)
        {
            return source == null ? default : select(source);
        }
        return new Equatable<TValue>(checkedSelect(Actual, select), checkedSelect(Expected, selectExpected));
    }
}
