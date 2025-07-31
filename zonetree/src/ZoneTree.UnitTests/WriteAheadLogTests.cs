using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.UnitTests;

public sealed class WriteAheadLogTests
{
    [Test]
    public void WalBasicTest()
    {
        var filePath = "./WalBasicTest.wal";
        if (File.Exists(filePath))
            File.Delete(filePath);
        var serializer = new UnicodeStringSerializer();
        var wal = new SyncFileSystemWriteAheadLog<string, string>(
            new ConsoleLogger(),
            new LocalFileStreamProvider(),
            serializer, serializer, filePath);
        var len = 1;
        for (var i = 0; i < len; ++i)
        {
            wal.Append("key" + i, "value" + i, i);
        }
        var result = wal.ReadLogEntries(false, false, true);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Exceptions, Is.Empty);
        Assert.That(result.Keys.Count, Is.EqualTo(len));
        Assert.That(result.Values.Count, Is.EqualTo(len));
        for (var i = 0; i < len; ++i)
        {
            Assert.That(result.Keys[i], Is.EqualTo("key" + i));
            Assert.That(result.Values[i], Is.EqualTo("value" + i));                
        }
        Assert.That(result.MaximumOpIndex, Is.EqualTo(len-1));
        wal.Drop();
    }

    [Test]
    public void TestWriteAheadLogCorruptionException()
    {
        Assert.DoesNotThrow(() => { new WriteAheadLogCorruptionException(0, null); });
        Assert.DoesNotThrow(() => { new WriteAheadLogCorruptionException(0, 
            new Dictionary<int, Exception> { }); });
    }
}
