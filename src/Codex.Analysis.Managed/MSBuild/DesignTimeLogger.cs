namespace Codex.Analysis.MSBuild;

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using BinaryLogger = Microsoft.Build.Logging.StructuredLogger.BinaryLogger;

public record DesignTimeLogger(string BinlogPath) : ILogger
{
    public ILogger ConsoleLogger = new ConsoleLogger();
    public BinaryLogger BinaryLogger = new BinaryLogger()
    {
        Parameters = BinlogPath.Replace("*", Guid.NewGuid().ToString())
    };

    public LoggerVerbosity Verbosity { get => ConsoleLogger.Verbosity; set => ConsoleLogger.Verbosity = value; }
    public string Parameters { get => ConsoleLogger.Parameters; set => ConsoleLogger.Parameters = value; }

    public void Initialize(IEventSource eventSource)
    {
        ConsoleLogger.Initialize(eventSource);
        BinaryLogger.Initialize(eventSource);

        eventSource.AnyEventRaised += EventSource_AnyEventRaised;
    }

    private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
    {
    }

    public void Shutdown()
    {
        BinaryLogger.Shutdown();
        ConsoleLogger.Shutdown();
    }
}
