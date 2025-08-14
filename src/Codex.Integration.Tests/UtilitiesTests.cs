using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Codex.Application.Verbs;
using Codex.Lucene.Search;
using Codex.ObjectModel;
using Codex.ObjectModel.CompilerServices;
using Codex.ObjectModel.Implementation;
using Codex.Search;
using Codex.Storage;
using Codex.Utilities;
using CommunityToolkit.HighPerformance;
using DotNext;
using DotNext.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using Microsoft.CodeAnalysis;
using Tenray.ZoneTree.Serializers;
using Xunit.Abstractions;
using Extent = Codex.Utilities.Extent;

namespace Codex.Integration.Tests;

using static Codex.ObjectModel.GlyphUtilities;
using SmallIntArray = ValueArray<int, T5<int>>;

public partial record UtilitiesTests(ITestOutputHelper output) : CodexTestBase(output)
{
    private static ISpanSerializer<SmallIntArray> ValueArraySerializer = new BinaryItemSerializer<SmallIntArray>();


    [Fact]
    public void AsymmetricEncryptionRoundtrip()
    {
        var data = EncryptionUtilities.GetGeneratedPassword();

        var keys = EncryptionUtilities.GenerateAsymmetricKeys();

        var encrypted = EncryptionUtilities.EncryptWithPublicKey(textToEncrypt: data, publicKeyBase64: keys.PublicKey);

        var decrypted = EncryptionUtilities.DecryptWithPrivateKey(textToDecrypt: encrypted, privateKeyBase64: keys.PrivateKey);

        decrypted.Should().Be(data);
    }

    [Fact]
    public void AsymmetricEncryptionZipRoundtrip()
    {
        var testDir = Path.Combine(GetTestOutputDirectory(clean: true), "sub");
        Directory.CreateDirectory(testDir);
        var keys = EncryptionUtilities.GenerateAsymmetricKeys();

        var data = EncryptionUtilities.GetGeneratedPassword();

        string fileName = "data.txt";
        File.WriteAllText(Path.Combine(testDir, fileName), data);

        var zipFileName = testDir + ".zip";
        MiscUtilities.CreateZipFromDirectory(
            testDir,
            zipFileName,
            publicKey: keys.PublicKey,
            generatedPassword: new(out var generatedPassword));

        using var zipFile = new ZipFile(zipFileName);
        var zipPassword = MiscUtilities.TryGetZipPassword(zipFile, keys.PrivateKey);

        zipPassword.Should().Be(generatedPassword);

        zipFile.Password = zipPassword;

        using var stream = zipFile.GetInputStream(zipFile.GetEntry(fileName));
        var readData = stream.ReadAllText();
        readData.Should().Be(data);
    }

    [Fact]
    public void GlyphNumbers()
    {
        //foreach (var group in Enum.GetValues<Glyph>().Select(g =>
        //{
        //    int num = 247 + (int)g;
        //    var group = StandardGlyphGroup.GlyphGroupUnknown;
        //    try
        //    {
        //        num = g.GetGlyphNumber();
        //        group = GlyphUtilities.GetStandardGlyphGroup(g);
        //    }
        //    catch { }
        //    return (glyph: g, num, group);
        //}).OrderBy(t => t.num).GroupBy(g => g.group))
        //{
        //    output.WriteLine($"// {group.Key}");
        //    foreach (var item in group)
        //    {
        //        output.WriteLine($"{item.glyph} = {item.num},");
        //    }
        //    output.WriteLine("");
        //}
    }

    [Fact]
    public void EntityCopyTest()
    {
        IDefinitionSymbol source = new DefinitionSymbol()
        {
            ShortName = "Hello",
            AbbreviatedName = "Abbreviation"
        };

        for (int i = 0; i < 2; i++)
        {
            var copy = i == 0
                ? new DefinitionSymbol(source)
                : new DefinitionSymbol().Apply(source);

            copy.ShortName.Should().NotBeNullOrEmpty();
            copy.ShortName.Should().Be(source.ShortName);
            copy.AbbreviatedName.Should().Be(source.AbbreviatedName);
            copy.ContainerQualifiedName.Should().Be(source.ContainerQualifiedName);
        }
    }

    [Fact]
    public void BaseEntityCopyTest()
    {
        var original = new ReferenceSearchModel();

        var clone = new ReferenceSearchModel(original);

        ISourceFileBase source = new SourceFile()
        {
            Info = new SourceFileInfo()
            {
                ProjectId = "hello"
            },
            Content = "Test content"
        };

        Parallel.For(0, 100, i =>
        {
            var copy = new ChunkedSourceFile(source);

            copy.Info.ProjectId.Should().Be(source.Info.ProjectId);
            copy.Info.ProjectRelativePath.Should().Be(source.Info.ProjectRelativePath);
        });


        var copy2 = new ChunkedSourceFile(source);

    }

    [Fact]
    public void ValueArrayTest()
    {
        var test1 = DocumentVisitor.GetInt64Term(0).Length.Should().Be(0);
        var test2 = DocumentVisitor.GetInt64Term(1).Length.Should().Be(2);
        var test3 = DocumentVisitor.GetInt64Term(ushort.MaxValue).Length.Should().Be(3);
        var test4 = DocumentVisitor.GetInt64Term(long.MaxValue).Length.Should().Be(9);

        var array = Create(10, 100, 5234, int.MaxValue, int.MinValue);
        var bytes = Serialize(array);

        var truncArray = array with { Length = 4 };
        var bytes2 = Serialize(truncArray);

        bytes.Length.Should().BeGreaterThan(bytes2.Length);

        int compareResult = ValueArraySerializer.Compare(array, truncArray);
        compareResult.Should().BeGreaterThan(0);

        array[0] = 1;
        compareResult = ValueArraySerializer.Compare(array, truncArray);
        compareResult.Should().BeLessThan(0);
    }

    private SmallIntArray Create(params int[] values)
    {
        var result = ValueArraySerializer.Deserialize(values.AsSpan().Cast<int, byte>());

        result.Length.Should().Be(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            int expected = values[i];
            int actual = result[i];
            actual.Should().Be(expected);
        }

        return result;
    }

    private ReadOnlySpan<byte> Serialize(in SmallIntArray array)
    {
        var bytes = ValueArraySerializer.Serialize(array);
        bytes.Length.Should().Be(array.Length * sizeof(int));
        return bytes;
    }

    [Fact]
    public void TestFeatures()
    {
        Features.TestBoolFeature.Value.Should().BeTrue();

        Environment.SetEnvironmentVariable(Features.FeatureEnvPrefix + nameof(Features.TestBoolFeature), "false");
        Features.GetFeaturesByName();
        Features.TestBoolFeature.Value.Should().BeFalse();

        Environment.SetEnvironmentVariable(Features.FeatureEnvPrefix + nameof(Features.TestBoolFeature), "INVALID");
        Features.GetFeaturesByName();
        Features.TestBoolFeature.Value.Should().BeFalse();
    }

    [Fact]
    public void TopMaxHeapTests()
    {
        int maxQueueSize = 200;
        int topCount = 10;
        var random = new Random(1203);
        for (int i = 0; i < 1000; i++)
        {
            var randomCount = random.Next(0, maxQueueSize);

            var randomStart = random.Next(0, randomCount);

            var randomValues = Enumerable.Range(0, randomCount)
                .Select(_ => random.Next(0, 1000))
                .Distinct()
                .ToArray();

            var sortedValues = ImmutableSortedSet.CreateBuilder<int>();

            var queue = new TopMinHeap<int>(topCount);

            foreach ((var value, var index) in randomValues.WithIndices())
            {
                queue.Push(value);

                sortedValues.Add(value);
                if (sortedValues.Count > topCount)
                {
                    sortedValues.Remove(sortedValues.Max);
                }

                if (index >= randomStart)
                {
                    queue.Count.Should().Be(sortedValues.Count);
                    queue.Min.Should().Be(sortedValues.Min);

                    queue.Verify();

                    if (random.Next(0, 2) == 0)
                    {
                        if (random.Next(0, 2) == 0)
                        {
                            queue.TryGetMin(out var min).Should().Be(true);
                            min.Should().Be(sortedValues.Min);
                            sortedValues.Remove(min);
                        }

                        queue.Verify();
                    }

                    if (sortedValues.Count > 0)
                    {
                        queue.Count.Should().Be(sortedValues.Count);
                        queue.Min.Should().Be(sortedValues.Min);
                    }
                }
            }
        }
    }

    [Fact]
    public void ArrayBuilderSetTest()
    {
        var set = new ArrayBuilderSet<(int key, int value)>(
            Compare.SelectEquality(((int key, int value) t) => t.key));

        ref var entry = ref set.Add((10, 100), out var added);
        entry.key.Should().Be(10);
        entry.value.Should().Be(100);
        added.Should().BeTrue();

        entry.value = 200;

        ref var getEntry = ref set.Add((10, -1), out added);
        getEntry.key.Should().Be(10);
        getEntry.value.Should().Be(200);
        added.Should().BeFalse();

        bool same = Unsafe.AreSame(ref entry, ref getEntry);
        same.Should().BeTrue();
    }

    [Fact]
    public void MinHeapTest()
    {
        int maxQueueSize = 2000;
        var random = new Random(1203);
        for (int i = 0; i < 100; i++)
        {
            var randomCount = random.Next(0, maxQueueSize);

            var randomStart = random.Next(0, randomCount);

            var randomValues = Enumerable.Range(0, randomCount)
                .Select(_ => random.Next(0, 1000))
                .Distinct()
                .ToArray();

            var sortedValues = ImmutableSortedSet.CreateBuilder<int>();

            var queue = new MinHeap<int>();

            foreach ((var value, var index) in randomValues.WithIndices())
            {
                queue.Push(value);
                sortedValues.Add(value);
                if (index >= randomStart)
                {
                    queue.Count.Should().Be(sortedValues.Count);
                    queue.Min.Should().Be(sortedValues.Min);

                    queue.Verify();

                    if (random.Next(0, 2) == 0)
                    {
                        queue.TryGetMin(out var min).Should().Be(true);
                        min.Should().Be(sortedValues.Min);
                        sortedValues.Remove(min);

                        queue.Verify();
                    }

                    if (sortedValues.Count > 0)
                    {
                        queue.Count.Should().Be(sortedValues.Count);
                        queue.Min.Should().Be(sortedValues.Min);
                    }
                }
            }
        }
    }

    [Fact]
    public void EnumTests()
    {
        FileAttributes attributes = FileAttributes.Directory;
        var combined = attributes.Or(FileAttributes.Archive);
        combined.Should().Be(FileAttributes.Directory | FileAttributes.Archive);
    }

    [Fact]
    public void SetBits()
    {
        TestSetBits<ulong>();
        TestSetBits<long>();
        TestSetBits<int>();
        TestSetBits<byte>();
        TestSetBits<ushort>();
        TestSetBits<short>();
        TestSetBits<short>();
        TestSetBits<uint>();
    }

    private void TestSetBits<TInt>()
            where TInt : unmanaged, IBinaryInteger<TInt>, IEqualityOperators<TInt, TInt, bool>, IMinMaxValue<TInt>
    {
        var random = new Random(1203);
        for (int i = 0; i < 100; i++)
        {
            testSetBits(random.NextInt64());
        }

        testSetBits(0);
        testSetBits(1);
        testSetBits(-1);
        testSetBits(long.MaxValue);

        testSetBits(long.CreateTruncating(TInt.MaxValue));
        testSetBits(long.CreateTruncating(TInt.MinValue));
        testSetBits(long.CreateTruncating(TInt.MinValue + TInt.One));
        testSetBits(long.CreateTruncating(TInt.MaxValue - TInt.One));

        void testSetBits(long longValue)
        {
            var value = TInt.CreateTruncating(longValue);

            var setBits = IntHelpers.EnumerateSetBits<TInt>(value).Select(i => int.CreateTruncating(i)).ToArray();

            List<int> expectedSetBits = new List<int>();
            var size = Unsafe.SizeOf<TInt>() * 8;

            for (int i = 0; i < size; i++)
            {
                var flag = TInt.One << i;
                if (value.HasFlag(flag))
                {
                    expectedSetBits.Add(i);
                }
            }

            setBits.Should().BeEquivalentTo(expectedSetBits);
        }
    }

    [Fact]
    public void TestReferenceKindRank()
    {
        var set = EnumData<ReferenceKind>.Values.ToHashSet();

        int maxRank = 0;

        foreach ((var kind, var rank) in ReferenceKindExtensions.ReferenceKindPreferenceList.WithIndices())
        {
            kind.GetPreference().Should().Be(rank);
            set.Remove(kind);
            maxRank = Math.Max(maxRank, rank);
        }

        int? defaultPreference = null;
        foreach (var kind in set)
        {
            var preference = kind.GetPreference();
            defaultPreference ??= preference;
            defaultPreference.Value.Should().Be(preference);

            preference.Should().BeGreaterThan(maxRank);
        }
    }

    [Fact]
    public void ReferenceKindSet()
    {
        var kinds = new ReferenceKindSet();
        kinds |= ReferenceKind.Constructor;

        kinds.Contains(ReferenceKind.Constructor).Should().BeTrue();
        kinds.Count.Should().Be(1);

        kinds.Add(ReferenceKind.Read);
        kinds.Count.Should().Be(2);
        kinds.Contains(ReferenceKind.Read).Should().BeTrue();

        TestKindSet(ReferenceKind.Constructor);
        TestKindSet(ReferenceKind.Read, ReferenceKind.Definition, ReferenceKind.Constructor);

        TestKindSet(ReferenceKind.None, ReferenceKind.TypeForwardedTo, ReferenceKind.DerivedType, ReferenceKind.InterfaceInheritance);
    }

    private static void TestKindSet(params ReferenceKind[] kinds)
    {
        var hs = new HashSet<ReferenceKind>();
        var rs = new ReferenceKindSet();

        var rank = int.MaxValue;

        for (int i = 0; i < 2; i++)
        {
            foreach (var kind in kinds)
            {
                hs.Add(kind);
                rs.Add(kind);

                rs.Count.Should().Be(hs.Count);

                rs.Enumerate().Should().BeEquivalentTo(hs.Order());

                rank = Math.Min(rank, kind.GetPreference());
                rs.GetPreference().Should().Be(rank);
            }
        }
    }

    [Fact]
    public void ReferenceEquatable()
    {
        HashSet<ReferenceEquatable<RefEqBase>> set = new();
        set.Add(new(new(0, 1)));
        set.Add(new(new(1, 1)));
        set.Add(new(new(0, 1)));

        set.Count.Should().Be(3);
    }

    public record RefEqBase(int Key, int Value)
    {

    }

    [Fact]
    public void IntArrayTest()
    {
        var array = IntArray.From(new int[] { 1 << 17 });
    }

    [Fact]
    public async Task ValueTaskCompletionSourceTests()
    {
        var tcs = new ValueTaskCompletionSource();
        tcs.Reset();
        tcs.TrySetResult();

        var task = tcs.CreateTask().AsTask();
        await task;
        await task;
        //await tcs.GetTask();
    }

    [Fact]
    public void CompressionTest()
    {
        var input = Encoding.UTF8.GetBytes("\n");
        byte[] compressed = GetCompressedFrame(input);
        byte[] compressedEmpty = GetCompressedFrame(new byte[0]);
        byte[] compressed2 = GetCompressedFrame(new byte[] { 10, 10 });

        var pickled = LZ4Pickler.Pickle(input);

        var compressedChain = Enumerable.Range(0, 10).SelectMany(i => compressed).ToArray();

        //var unpickledChain = LZ4Pickler.Unpickle(compressedChain);

        var uncompressedChain = LZ4Stream.Decode(new MemoryStream(compressedChain), 0).ReadAllBytes();
    }

    private static byte[] GetCompressedFrame(byte[] input)
    {
        var compressionStream = new MemoryStream();
        using (var writer = LZ4Frame.Encode(compressionStream, leaveOpen: true))
        {
            writer.OpenFrame();
            writer.WriteManyBytes(input);
            writer.CloseFrame();
        }

        compressionStream.Position = 0;
        var compressed = compressionStream.ToArray();
        return compressed;
    }

    [Fact]
    public void RangeTest()
    {
        var s = new[] { "apple", "banana", "cranberry" };
        var range = s.GetPrefixRange("ban", s => s);
        var item = range[0];

        var starts = new Extent[] { (50, 62), (71, 74), (80, 83),
            (124, 135), (138, 150), (166, 169), (175, 178), (212, 235), (263, 286), (311, 327), (343, 347), (348, 364),
            (365, 376), (402, 405), (422, 423), (443, 444), (478, 482), (483, 499), (500, 512),
            (538, 541), (558, 559), (579, 580) };

        var spanRange = starts.GetRange(422, (pos, r) => -r.CompareTo(pos), (pos, r) => -r.CompareTo(pos));
    }

    [Fact]
    public void TestContainerNameFieldTransform()
    {
        (string Original, string Expected)[] transforms = new[]
        {
            ("CTP.FNS.GT<T1, TNext, T3Arg>.GN<TKey>", "ctp.fns.gt.gn`1"),
            ("CTP.FNS.GT<T1, TNext, T3Arg>", "ctp.fns.gt`3"),
            ("CTP.FNS.GT<T1, TNext, T3Arg", "ctp.fns.gt`"),
        };

        foreach (var item in transforms)
        {
            var actual = SearchUtilities.GetNameTransformedValue(item.Original);
            Assert.Equal(item.Expected, actual);
        }
    }

    [Fact]
    public void TestContainerNameFieldValues()
    {
        (string Original, string[] Expected)[] transforms = new[]
        {
            ("CTP.FNS.GT<T1, TNext, T3Arg>.GN<TKey>", new[] { "ctp.fns.gt.gn`1", "fns.gt.gn`1", "gt.gn`1", "gn`1" }),
            ("CTP.FNS.GT<T1, TNext, T3Arg.GN<TKey>", new[] { "ctp.fns.gt.gn`1", "fns.gt.gn`1", "gt.gn`1", "gn`1" }),
            ("CTP.FNS.GT.GN<TKey>", new[] { "ctp.fns.gt.gn`1", "fns.gt.gn`1", "gt.gn`1", "gn`1" }),
            ("CTP.FNS.GT.GN", new[] { "ctp.fns.gt.gn", "fns.gt.gn", "gt.gn", "gn" }),
            ("CTP.FNS.GT<T1, TNext, T3Arg>", new[] { "ctp.fns.gt`3", "fns.gt`3", "gt`3" })
        };

        foreach (var item in transforms)
        {
            var actual = SearchUtilities.EnumerateContainerQualifiedNameUnhashedFieldValues(item.Original).ToArray();
            Assert.Equal(item.Expected, actual);

            var hashedValues = SearchUtilities.EnumerateContainerQualifiedNameFieldValues(item.Original).ToArray();
        }
    }

    [Fact]
    public void TestShortNameSplitting()
    {
        var text = "IDefinitionSymbol";
        IndexingUtilities.AccumulateAbbreviationCharacters(text, 0, t =>
        {
            var substring = text.Substring(t.index);
            return 0;
        });
    }

    [Fact]
    public void GitIgnoreTests()
    {
        var gitIgnore = GitIgnore.Parse(new StringReader("dotnet/roslyn"), invert: true);

        Assert.True(gitIgnore.Includes("dotnet/roslyn"));
        Assert.True(gitIgnore.Includes("dotnet/roslyn/sub"));
        Assert.True(gitIgnore.Includes("dotnet/roslyn/sub"));


    }

    [Fact]
    public async Task ApplyDirectoryChanges()
    {
        var testOutputDir = GetTestOutputDirectory(clean: true);

        var apply = new ApplyDirectoryChangesOperation()
        {
            SourceDirectory = Path.Combine(testOutputDir, "source"),
            TargetDirectory = Path.Combine(testOutputDir, "target"),
            MaskingDirectory = Path.Combine(testOutputDir, "mask"),
        };

        void write(string root, string relativePath, string contents)
        {
            if (contents != null)
            {
                var path = Path.Combine(root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, contents);
            }
        }

        await check(
            new[]
            {
                ("1.txt", "overwritten", "ERROR: unchanged", false),
                ("b/2.txt", "overwritten", "ERROR: unchanged", false),
                ("b/shouldbeunchanged.txt", "ERROR: this should be unchanged", "unchanged", true),
                ("b/shouldbedeleted.txt", null, "unchanged", true),
                ("shouldbedeletedaswell.txt", null, "unchanged", false),
            }
        );

        async Task check(params (string relativePath, string sourceText, string targetText, bool exclude)[] files)
        {
            foreach (var file in files)
            {
                write(apply.SourceDirectory, file.relativePath, file.sourceText);
                write(apply.TargetDirectory, file.relativePath, file.targetText);

                if (!file.exclude)
                {
                    write(apply.MaskingDirectory, file.relativePath, "masked");
                }
            }

            await apply.RunAsync();

            foreach (var file in files)
            {
                bool shouldExist = file.sourceText != null;
                var path = Path.Combine(apply.TargetDirectory, file.relativePath);
                bool exists = File.Exists(path);
                Logger.LogMessage($"{file.relativePath} Exists={exists}, ShouldExist={shouldExist}");
                exists.Should().Be(shouldExist, path);
                if (exists)
                {
                    var readText = File.ReadAllText(path);
                    var expectedText = file.exclude ? file.targetText : file.sourceText;
                    Logger.LogMessage($"{file.relativePath} Content={readText}, ExpectedContent={expectedText}");
                    readText.Should().NotContain("ERROR");
                    readText.Should().Be(expectedText, path);
                }
            }
        }
    }
}
