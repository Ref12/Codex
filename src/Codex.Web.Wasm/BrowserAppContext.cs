using System.Runtime.InteropServices.JavaScript;
using System;
using System.Threading;
using Codex.Utilities;
using System.Threading.Tasks;
using Codex.Web.Wasm;
using Codex;
using Codex.Web.Common;

namespace System;

public partial class BrowserAppContext
{
    public static SynchronizationContext SynchronizationContext { get; private set; }

    public static int MainThreadId { get; private set; }

    public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;

    public static async Task<WebProgramArguments> InitializeAsync(bool isSingleThreaded = false)
    {
        await CodexJsRuntime.StartAsync();

        var argsJson = CodexJsRuntime.GetApplicationArgumentsJson();
        Console.WriteLine($"Arguments: {argsJson}");

        var args = argsJson.DeserializeEntity<WebProgramArguments>();
        args.Process();

        SynchronizationContext = SynchronizationContext.Current;
        MainThreadId = Thread.CurrentThread.ManagedThreadId;
        return args;

    }

    private static IInnerHttpClient GetClient(HttpClientKind kind)
    {
        var client = new BrowserHttpClientWrapper();
        return client;
    }

    public static TaskUtilities.SynchronizationContextAwaitable SwitchToMainThread()
    {
        return SynchronizationContext.SwitchTo();
    }
}