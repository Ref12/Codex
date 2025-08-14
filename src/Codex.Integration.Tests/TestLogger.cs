using System.Text;
using Codex.Logging;
using Xunit.Abstractions;

namespace Codex.Integration.Tests;

public class TestLogger : TextLogger
{
    public TestLogger(ITestOutputHelper output)
        : base(new TestOutputWriter(output))
    {
        Output = output;
    }

    public ITestOutputHelper Output { get; }

    protected override void WriteLineCore(string text)
    {
        try
        {
            Output.WriteLine(text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Could not log message: {text} due to exception\n{ex}");
        }
    }
}

public class TestOutputWriter : StringWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public ITestOutputHelper Output { get; }

    public TestOutputWriter(ITestOutputHelper output)
    {
        Output = output;
    }

    public override void WriteLine(string value)
    {
        Output.WriteLine(value);
    }

    public override void WriteLine()
    {
        Flush();
    }

    public override void Flush()
    {
        var sb = base.GetStringBuilder();
        Output.WriteLine(sb.ToString());
        sb.Clear();
    }
}
