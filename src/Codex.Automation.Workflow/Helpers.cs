using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.Automation.Workflow
{
    public static class Helpers
    {
        public static T With<T>(this T item, Action<T> modification)
        {
            modification(item);
            return item;
        }

        public static void Log(string message, [CallerMemberName]string method = null)
        {
            Console.WriteLine($"{method}: {message}");
        }

        public static bool RunProcess(string exePath, string arguments, IDictionary<string, string> envVars = null,
            AsyncOut<int>? exitCode = null, string workingDirectory = null, IReadOnlyDictionary<string, string> expansions = null, TaskCompletionSource<Process> proc = null,
            CancellationToken token = default, bool admin = false)
        {
            proc ??= new();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && exePath.EndsWithIgnoreCase(".exe"))
            {
                arguments = string.Join(" ", QuoteIfNecessary(Path.ChangeExtension(exePath, ".dll")), arguments);
                exePath = "dotnet";
            }

            Log($"Running Process (working dir = '{workingDirectory}'): {exePath} {arguments}");

            if (expansions != null)
            {
                foreach (var entry in expansions)
                {
                    arguments = arguments.Replace(entry.Key, entry.Value);
                }
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var process = Process.Start(new ProcessStartInfo(exePath, arguments)
                {
                    UseShellExecute = false
                }
                .With(info =>
                {
                    if (admin)
                    {
                        info.Verb = "runas";
                    }
                    //info.Environment.Clear();
                    info.RedirectStandardOutput = Features.RedirectWorkflowStandardOut;
                    info.RedirectStandardError = Features.RedirectWorkflowStandardOut;

                    if (!string.IsNullOrEmpty(workingDirectory))
                    {
                        info.WorkingDirectory = workingDirectory;
                    }

                    if (envVars != null)
                    {
                        foreach (var kvp in envVars)
                        {
                            if (!string.IsNullOrEmpty(kvp.Value))
                            {
                                info.Environment[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }));

                proc.TrySetResult(process);

                if (Features.RedirectWorkflowStandardOut)
                {
                    process.OutputDataReceived += (sender, data) =>
                    {
                        Console.Out.WriteLine(data.Data ?? "");
                    };

                    process.ErrorDataReceived += (sender, data) =>
                    {
                        Console.Out.WriteLine(data.Data ?? "");
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                using var _ = token.Register(() => process.Kill(true));

                process.WaitForExit();
                bool success = process.ExitCode == 0;
                Log($"Run completed with exit code [Elapsed: {stopwatch.Elapsed}] (Succeeded: {success}) '{process.ExitCode}': {exePath} {arguments}");
                exitCode?.Set(process.ExitCode);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                exitCode?.Set(-1);
                proc.TrySetException(ex);
                return false;
            }
        }

        public static bool Invoke(string processExe, string[] arguments)
        {
            var processArgs = string.Join(" ", arguments.Select(QuoteIfNecessary));
            return RunProcess(processExe, processArgs);
        }

        public static string QuoteIfNecessary(string arg)
        {
            return arg.QuoteIfNeeded();
        }
    }
}
