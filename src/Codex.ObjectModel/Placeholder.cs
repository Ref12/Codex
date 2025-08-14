using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    [GeneratorExclude]
    public enum Dbg
    {
        None = 0,
        StableBlockIndexing = 1 << 0,
    }

    /// <summary>
    /// Class for marking places to fill in while implementing
    /// </summary>
    public static class Placeholder
    {
        public const bool IsCommitModelEnabled = true;

        public static bool Debug(Dbg d)
        {
#if DEBUG_LOCAL
            if ((Features.DebugScenarios & d) == d)
            {
                LaunchDebugger();
                return true;
            }
#endif

            return false;
        }

        public static T DebugValue<T>(T debugValue)
        {
#if DEBUG_LOCAL
            return debugValue;
#else
            return default(T);
#endif
        }

        public static T Value<T>(string message = null)
        {
            return default(T);
        }

        public static T MarkForRemoval<T>(this T value)
        {
            return value;
        }

        public static T MarkForRemoval<T>(this T value, string message = null)
        {
            return value;
        }

        public static bool MissingFeature(string message = null)
        {
            return false;
        }

        public static Task NotImplementedAsync(string message = null)
        {
            throw new NotImplementedException();
        }

        public static Exception NotImplementedException(string message = null)
        {
            throw new NotImplementedException();
        }

        [Conditional("NOTIMPLEMENTED")]
        public static void NotImplemented(string message = null)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Indicates that a test value is temporarily substituted here for real value
        /// </summary>
        public static T TestValue<T>(T testValue, T realValue)
        {
            return testValue;
        }

        public static T Todo<T>(this T value, string message)
        {
            return value;
        }

        public static T CheckValue<T>(this T value, Func<T, bool> assertion)
        {
            if (!assertion(value))
            {

            }

            return value;
        }

        [Conditional("TODO")]
        public static void Todo(string message = null)
        {
            throw new NotImplementedException();
        }

        [System.AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
        public sealed class TodoAttribute : Attribute
        {
            // This is a positional argument
            public TodoAttribute(string message)
            {
            }
        }

        public static T EnsureNotNull<T>(this T value, [CallerLineNumber]int line = 0, [CallerArgumentExpression(nameof(value))]string exp = null)
            where T : class
        {
            if (value == null)
            {
                Debugger.Launch();
            }

            return value;
        }

        public static object RunIf(bool condition, Action action)
        {
            if (condition) action.Invoke();
            return null;
        }

        public static object DebugIf(bool condition, [CallerMemberName] string message = null)
        {
            if (condition)
            {
                Debugger.Launch();
            }

            return null;
        }

        public static bool DebugWhen(int id = 0, [CallerMemberName]string message = null)
        {
            if (id == 1)
            {
                Debugger.Launch();
            }
            return true;
        }

        private static int _debugLock = 0;
        //[Conditional("DEBUGGER")]
        //[Conditional("DEBUG_LOCAL")]
        public static bool LaunchDebugger(bool breakAlways = false, int sleepSeconds = 0)
        {
            var result = launch();
            if (result)
            {
                Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                Debugger.Launch();
            }
            return result;

            bool launch()
            {
                if (Features.IsTest)
                {
                    if (breakAlways || !Debugger.IsAttached)
                    {
                        if (Interlocked.CompareExchange(ref _debugLock, 1, 0) == 0)
                        {
                            if (breakAlways || !Debugger.IsAttached)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
        }

        [Conditional("DEBUGLOG")]
        public static void Trace(
            string message = null,
            [CallerMemberName]string name = null,
            [CallerLineNumber]int line = -1,
            [CallerFilePathAttribute] string file = null)
        {
            TraceCore(message, name, line, file);
        }

        [Conditional("TRACE2")]
        public static void Trace2(
            object message = null,
            [CallerMemberName] string name = null,
            [CallerLineNumber] int line = -1,
            [CallerFilePathAttribute] string file = null,
            bool includeStack = false)
        {
            TraceCore(message?.ToString(), name, line, file, includeStack);
        }

        private static void TraceCore(string message, string name, int line, string file, bool includeStack = false)
        {
            var ct = Thread.CurrentThread;
            string now = DateTime.UtcNow.ToString();
            var fileName = StringExtensions.SubstringAfterLastIndexOfAny(file, @"\");
            string stack = "";
            if (includeStack)
            {
                try
                {
                    throw new Exception();
                }
                catch (Exception ex)
                {
                    stack = $"\n{ex}";
                    throw;
                }
            }
            Console.WriteLine($"[{now}] T{ct.ManagedThreadId}-{ct.Name} -- {fileName}:{name}#{line} '{message}'{stack}");
        }

        [Conditional("DEBUGLOG")]
        public static void DebugLog(string message)
        {
            DebugLogCore(message);
        }

        [Conditional("DEBUGLOG")]
        public static void DebugLog2(string message)
        {
            DebugLogCore(message);
        }

        [Conditional("DEBUGLOG3")]
        public static void DebugLog3(string message)
        {
            DebugLogCore(message);
        }

        public static void DebugLogCore(string message)
        {
            var ct = Thread.CurrentThread;
            string now = DateTime.UtcNow.ToString();
            Console.WriteLine($"[{now}] T{ct.ManagedThreadId}-{ct.Name} -- {message}");
        }

        public static void InfoLog(string message)
        {
            var ct = Thread.CurrentThread;
            string now = DateTime.UtcNow.ToString();
            Console.WriteLine($"[{now}] T{ct.ManagedThreadId}-{ct.Name} -- {message}");
        }
    }
}
