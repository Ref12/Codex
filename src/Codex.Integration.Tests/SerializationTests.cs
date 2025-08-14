using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Azure.Storage.Blobs.Models;
using Codex.Application.Verbs;
using Codex.Lucene;
using Codex.Lucene.Formats;
using Codex.ObjectModel;
using Codex.ObjectModel.Attributes;
using Codex.ObjectModel.Implementation;
using Codex.Sdk.Search;
using Codex.Sdk.Utilities;
//using Codex.Sdk.Utilities;
using Codex.Storage.Store;
using Codex.Utilities;
using Codex.Utilities.Serialization;
using MessagePack;
using Xunit;
using Xunit.Abstractions;
using static System.Net.Mime.MediaTypeNames;
using Extent = Codex.Utilities.Extent;

namespace Codex.Integration.Tests;

public partial class SerializationTests(ITestOutputHelper Output)
{
    [Fact]
    public void NullSerializationTest()
    {
        CodeSymbol symbol = null;
        var bytes = symbol.PackSerializeEntity();

        var deserialized = MessagePacker.PackDeserializeEntity<CodeSymbol>(bytes);
    }

    [Fact]
    public void StringEnum()
    {
        StringEnum<SymbolKinds> original = SymbolKinds.File;
        var bytes = MessagePacker.PackSerializeEntity(original);
        var deserialized = MessagePacker.PackDeserializeEntity<StringEnum<SymbolKinds>>(bytes);
        Assert.Equal(original.IntegralValue, deserialized.IntegralValue);
        Assert.NotNull(deserialized.IntegralValue);
        Assert.Equal(original.StringValue, deserialized.StringValue);
        Assert.Equal(1, bytes.Length);

        original = "HelloKind";
        bytes = MessagePacker.PackSerializeEntity(original);
        deserialized = MessagePacker.PackDeserializeEntity<StringEnum<SymbolKinds>>(bytes);
        Assert.Equal(original.IntegralValue, deserialized.IntegralValue);
        Assert.Null(deserialized.IntegralValue);
        Assert.Equal(original.StringValue, deserialized.StringValue);
        Assert.NotEqual(1, bytes.Length);
    }

    [Fact]
    public void Enum()
    {
        var classificationNames = new ClassifiedExtent[] { new(ClassificationName.ClassName, 12), new(ClassificationName.ClassName, 57) };

        var result = classificationNames.AsReadOnlyList().PackSerializeEntity();
        Assert.Equal(7, result.Length);
    }

    [Fact]
    public void PropertyMapSerialization()
    {
        var intarray = IntArray.From(new[] { 1, 2 });
        var ias = JsonSerializationUtilities.SerializeEntity(intarray);

        var propMap = new PropertyMap()
        {
            { "hello", "world" },
            { PropertyKey.Checksum_Sha1, "test" }
        };

        Dictionary<StringEnum<PropertyKey>, int> props = propMap.ToDictionary(k => k.Key, k => k.Value.Length);

        var mapString = JsonSerializationUtilities.SerializeEntity(propMap);
        var dictString = JsonSerializationUtilities.SerializeEntity(props);

        var desm = JsonSerializationUtilities.DeserializeEntity<Dictionary<StringEnum<PropertyKey>, int>>(dictString);
        var des = JsonSerializationUtilities.DeserializeEntity<PropertyMap>(mapString);
    }

    [Fact]
    public void PropertyMapPackSerialization()
    {
        var propMap = new PropertyMap()
        {
            { "hello", "world" }
        };

        Dictionary<StringEnum<PropertyKey>, string> props = new(propMap);

        var adapterMapString = MessagePacker.PackSerializeEntity<IPropertyMap>(propMap);
        var mapString = MessagePacker.PackSerializeEntity(propMap);
        var dictString = MessagePacker.PackSerializeEntity(props);

        Assert.Equal(adapterMapString, mapString);
        Assert.Equal(adapterMapString, dictString);

        var intarray = IntArray.From(new[] { 1, 2 });
        var ias = MessagePacker.PackSerializeEntity(intarray);
    }

    [Fact]
    public void SketchSerialization()
    {
        var sketch = new ProjectReferenceMinCountSketch();

        var stringValue = JsonSerializationUtilities.SerializeEntity(sketch);
    }

    [Fact]
    public void CustomExclusionSerialization()
    {
        var definition = new DefinitionSymbol()
        {
            Comment = "original",
        };

        var project = new AnalyzedProjectInfo()
        {
            Definitions =
            {
                definition
            }
        };

        var sourceInfo = new SourceFileInfo()
        {
            ProjectRelativePath = "testpath",
            RepoRelativePath = "repopath",
            RepositoryName = "reponame",
            ProjectId = "testprojectId",
            Size = 10,
            EncodingInfo = EncodingName.utf_16
        };

        var si_block = JsonSerializationUtilities.SerializeEntity(sourceInfo, ObjectStage.BlockIndex);
        var si_all = JsonSerializationUtilities.SerializeEntity(sourceInfo, ObjectStage.All);

        var pstring = JsonSerializationUtilities.SerializeEntity(project, ObjectStage.OptimizedStore);

        var defString = JsonSerializationUtilities.SerializeEntity(definition);
        var idefString = JsonSerializationUtilities.SerializeEntity<IDefinitionSymbol>(definition);
        var refString = JsonSerializationUtilities.SerializeEntity<ReferenceSymbol>(definition);
        var irefString = JsonSerializationUtilities.SerializeEntity<IReferenceSymbol>(definition);

        Assert.DoesNotContain("Definition", defString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Definition", idefString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Definition", refString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Definition", irefString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FieldListSerialization()
    {
        var s = ReferenceKindSet.AllKinds.PackSerializeEntity();

        var visitor = new FieldListVisitor(new());

        var json = @"{'definition':{'abbreviatedName':'GMTT','classifications':[4133,272,3621,272,2835,272,539,272,256,1307,272,256,1307,272,272,3361,272,539,272,256,539,272,272,272],'containerQualifiedName':'CodexTestProject.FullNameSearch.GenericType<T1, TNext, T3Arg>','containerTypeSymbolId':'z9ats7fyxwgp','displayName':'CodexTestProject.FullNameSearch.GenericType<T1, TNext, T3Arg>.GenericMethod<T2, T4>()','glyph':'MethodPublic','id':'wfkvbv2f2umm','kind':'Method','projectId':'CodexTestProject','shortName':'GenericMethod<T2, T4>','symbolDepth':1}}";

        var entity = json.Replace('\'', '"').DeserializeEntity<IDefinitionSearchModel>();

        SearchTypes.Definition.VisitFields(entity, visitor);

        var rvisitor = new FieldListVisitor(new());

        json = @"{'entityContentId':'o-ry6ll7djMwuPhfjCg-bg','entityContentSize':1304,'fileInfo':{'projectId':'CodexTestProject','projectRelativePath':'TestCases\\ReferenceKindsTest.cs','repoRelativePath':'src\\playground\\CodexTestProject\\TestCases\\ReferenceKindsTest.cs','repositoryName':'Codex2'},'referenceKind':1207959682,'references':{'spans':[{'info':{'referenceKind':'Partial'},'length':7,'lineNumber':17,'lineSpanStart':7,'lineSpanText':'public partial interface IHaveIndexer : IBase','start':265},{'info':{'referenceKind':'Definition'},'length':12,'lineNumber':17,'lineSpanStart':25,'start':283},{'info':{'referenceKind':'InterfaceImplementation','relatedDefinition':'rkkfcvdite5u'},'length':12,'lineNumber':33,'lineSpanStart':83,'lineSpanText':'public record HaveIndexer(int RecordArgProperty, bool RecordArgParamAndProperty) : IHaveIndexer','start':670},{'info':{},'length':12,'lineNumber':79,'lineSpanStart':29,'lineSpanText':'public void ReferenceIndexer(IHaveIndexer indexer)','start':1818},{'info':{'referenceKind':'Partial'},'length':7,'lineNumber':125,'lineSpanStart':7,'lineSpanText':'public partial interface IHaveIndexer','start':2636},{'info':{'referenceKind':'Definition'},'length':12,'lineNumber':125,'lineSpanStart':25,'start':2654}],'symbol':{'id':'yzqpi1mw98co','kind':'Interface','projectId':'CodexTestProject'}},'uid':'o-ry6ll7djMwuPhfjCg-bg'}";

        var rentity = json.Replace('\'', '"').DeserializeEntity<ReferenceSearchModel>();
        rentity.Symbol = entity.Definition;

        SearchTypes.Reference.VisitFields(rentity, rvisitor);

        var serialized = rvisitor.Fields.PackSerializeEntity();

        var deserialized = MessagePacker.PackDeserializeEntity<List<(string key, object value)>>(serialized);
    }

    [Fact]
    public void HashExclusionSerialization()
    {
        var def = new DefinitionSymbol()
        {
            Comment = "original",
            JsonRange = new Extent<IDefinitionSymbol>(new Extent(10, 22))
        };
        var entity = new DefinitionSearchModel()
        {
            Definition = def
        };

        var bytes = MessagePacker.PackSerializeEntity(entity, ObjectStage.Analysis);
        var bytes2 = MessagePacker.PackSerializeEntity(entity.Definition, ObjectStage.Analysis);

        var objContent1 = JsonSerializationUtilities.SerializeEntity((object)entity.Definition, ObjectStage.Hash);
        var objContent2 = JsonSerializationUtilities.SerializeEntity((object)entity, ObjectStage.Index);
        entity.PopulateContentIdAndSize(force: true);
        var expectedContent = entity.SerializeEntity(ObjectStage.Hash);
        var expectedHash = entity.EntityContentId;
        var expectedSize = entity.EntityContentSize;
        verify(changed: false);

        entity.StableId = 10;
        entity.EntityContentSize = 123421;
        entity.EntityContentId = MurmurHash.Random();
        entity.Uid = MurmurHash.Random();
        def.Comment = "changed";
        verify(changed: false);

        var newContent = entity.SerializeEntity(ObjectStage.Index);
        Assert.NotEqual(expectedContent, newContent);

        entity.Definition = new DefinitionSymbol()
        {
            ShortName = "test"
        };

        verify(changed: true);

        void verify(bool changed)
        {
            entity.PopulateContentIdAndSize(force: true);
            var newContent = entity.SerializeEntity(ObjectStage.Hash);
            var newHash = entity.EntityContentId;
            var newSize = entity.EntityContentSize;
            if (changed)
            {
                Assert.NotEqual(expectedContent, newContent);
                Assert.NotEqual(expectedHash, newHash);
                Assert.NotEqual(expectedSize, newSize);
            }
            else
            {
                Assert.Equal(expectedContent, newContent);
                Assert.Equal(expectedHash, newHash);
                Assert.Equal(expectedSize, newSize);
            }
        }
    }

    [Fact]
    public void SpanSerializationTests()
    {
        SpanWriter writer = new byte[10000].AsSpan();
        SpanReader reader32 = writer.Span;
        SpanReader reader64 = writer.Span;

        var values = new int[] { int.MinValue, int.MinValue >> 1, -23,
            -2, -1, 0, 1, 2, 7, 32, int.MaxValue >> 1, int.MaxValue };

        int written = 0;
        void write(ref SpanWriter writer, long value)
        {
            writer.WriteZigZag(value);
            Output.WriteLine($"Value = '{value}', Width = {writer.WrittenBytes.Length - written}");
            written = writer.WrittenBytes.Length;
        }

        foreach (var value in values)
        {
            write(ref writer, value);
            reader32.ReadInt32ZigZag().Should().Be(value);
            reader64.ReadInt64ZigZag().Should().Be(value);
        }

        var longValues = new long[] {
            long.MinValue, long.MinValue >> 1, int.MinValue * 2L,
            long.MaxValue, long.MaxValue >> 1, int.MaxValue * 2L };

        foreach ((long value, int index) in longValues.WithIndices())
        {
            write(ref writer, value);
            reader64.ReadInt64ZigZag().Should().Be(value);
        }
    }

    [Fact]
    public void Base64RoundtripTests()
    {
        var bytes = Guid.Empty.ToByteArray();
        Span<char> buffer = stackalloc char[100];
        Span<byte> byteBuffer = stackalloc byte[100];

        for (int i = 0; i < 100; i++)
        {
            var g = Guid.NewGuid();
            MurmurHash expected = new MurmurHash(g);
            var base64 = expected.ToBase64String();
            var parsed = MurmurHash.Parse(base64);
            Assert.Equal(expected, parsed);
        }
    }

    [Fact]
    public void ShouldSerializePropertySerialization()
    {
        var value = new DefinitionSymbol();
        var stringValue = JsonSerializationUtilities.SerializeEntity(value);
        var deserialized = JsonSerializationUtilities.DeserializeEntity<DefinitionSymbol>(stringValue);

    }

    [Fact]
    public void PrimitiveAsStringDeserialization()
    {
        FindDefinitionLocationArguments arguments = new FindDefinitionLocationArguments();
        JsonObject job = new JsonObject()
        {
            [nameof(FindDefinitionLocationArguments.ProjectId)] = "hello",
            [nameof(FindDefinitionLocationArguments.RequireLineTexts)] = (!arguments.RequireLineTexts).ToString(),
            [nameof(FindDefinitionLocationArguments.MaxResults)] = "12",

        };

        var options = JsonSerializationUtilities.GetOptions(ObjectStage.Index, JsonFlags.PrimitivesAsString);

        var result = job.Deserialize<FindDefinitionLocationArguments>(options);
        Assert.NotEqual(arguments.RequireLineTexts, result.RequireLineTexts);
        Assert.Equal(12, result.MaxResults);
    }

    [Fact]
    public void BasicSerialization()
    {
        var value = MessagePacker.PackSerializeEntity(MurmurHash.Random());

        var ts = new TextLineSpan()
        {
            LineSpanText = "hello"
        };

        var tss = JsonSerializationUtilities.SerializeEntity(ts, ObjectStage.Index);


        SourceFile sf = new SourceFile() { Content = "hello" };
        var sfs = JsonSerializationUtilities.SerializeEntity(sf, default);

        Repository repo = new Repository()
        {
            Name = "testrepo"
        };

        var model = ClassificationListModel.CreateFrom(new[] { new ClassificationSpan()
        {
            Start = 10,
            Length  = 5,
            Classification = "keyword",
        }});

        var modelString = JsonSerializationUtilities.SerializeEntity(model);

        var stringValue = JsonSerializationUtilities.SerializeEntity(repo);

        var deserialized = JsonSerializationUtilities.DeserializeEntity<IRepository>(stringValue);
        Assert.Equal(repo.Name, deserialized.Name);

        var rangeString = JsonSerializationUtilities.SerializeEntity<Extent<int>>(Extent.FromBounds(0, 10));
    }

    [Fact]
    public void StoredFilterSerialization()
    {
        var file = new PersistedStoredFilterSet()
        {
            FiltersByType =
            {
                {
                    SearchTypeId.Definition,
                    RoaringDocIdSet.From(new[] { 0, 1, 2 })
                }
            }
        };

        var stringValue = JsonSerializationUtilities.SerializeEntity(file, flags: JsonFlags.Indented);

        var deserialized = JsonSerializationUtilities.DeserializeEntity<PersistedStoredFilterSet>(stringValue);
        Assert.Equal(
            file.FiltersByType[SearchTypeId.Definition].Enumerate(),
            deserialized.FiltersByType[SearchTypeId.Definition].Enumerate());
    }

    [Fact]
    public void SerializeSerializesBaseTypeProperties()
    {
        var value = new ProjectFileLink()
        {
            FileId = "testFileId",
            ProjectId = "projectid"
        };

        var stringValue = JsonSerializationUtilities.SerializeEntity(value);

        var deserialized = JsonSerializationUtilities.DeserializeEntity<IProjectFileLink>(stringValue);
        Assert.Equal(value.ProjectId, deserialized.ProjectId);
    }

    [Fact]
    public void SerializeStoredBoundSourceFile()
    {
        var value = new StoredBoundSourceFile()
        {
            BoundSourceFile = new BoundSourceFile()
            {
                SourceFile = new SourceFile()
                {
                    Info = new SourceFileInfo()
                    {
                        RepositoryName = "myrepo"
                    }
                },
                References = new List<ReferenceSpan>()
                {
                    new ReferenceSpan()
                    {
                        Reference = new ReferenceSymbol()
                        {
                            ProjectId = "proj",
                            Id  = SymbolId.UnsafeCreateWithValue("abc")
                        },
                        Start = 10,
                        LineSpanText = "Hello world"
                    }
                }
            }
        };

        var stringValue = JsonSerializationUtilities.SerializeEntity(value, flags: JsonFlags.Indented);

        value.BeforeSerialize(true);

        var optStringValue = JsonSerializationUtilities.SerializeEntity(value, flags: JsonFlags.Indented);

        var deserialized = JsonSerializationUtilities.DeserializeEntity<StoredBoundSourceFile>(stringValue);

        Assert.Equal(value.BoundSourceFile.RepositoryName, deserialized.BoundSourceFile.RepositoryName);
    }

    public record Hello(string Message1);

    [Fact]
    public void SerializeCompressedReferenceList()
    {

        JsonSerializationUtilities.SerializeEntity(new Hello("Hi world"));

        var value = new StoredBoundSourceFile()
        {
            BoundSourceFile = new BoundSourceFile()
            {
                References = new List<ReferenceSpan>()
                {
                    new ReferenceSpan()
                    {
                        Reference = new ReferenceSymbol()
                        {
                            ProjectId = "proj",
                            Id  = SymbolId.UnsafeCreateWithValue("abc")
                        },
                        Start = 10,
                        LineSpanText = "Hello world"
                    }
                }
            }
        };

        value.BeforeSerialize(true);

        var optStringValue = JsonSerializationUtilities.SerializeEntity<IReferenceListModel>(value.CompressedReferences, flags: JsonFlags.Indented);
    }

    [Fact]
    public void PackSerializeCompressedReferenceList()
    {
        var value = new StoredBoundSourceFile()
        {
            BoundSourceFile = new BoundSourceFile()
            {
                References = new List<ReferenceSpan>()
                {
                    new ReferenceSpan()
                    {
                        Reference = new ReferenceSymbol()
                        {
                            ProjectId = "proj",
                            Id  = SymbolId.UnsafeCreateWithValue("abc")
                        },
                        Start = 10,
                        LineSpanText = "Hello world"
                    }
                }
            }
        };

        value.BeforeSerialize(true);

        var bytes = MessagePacker.PackSerializeEntity(value);
    }
}