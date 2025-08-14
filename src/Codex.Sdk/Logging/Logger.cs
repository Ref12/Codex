using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public abstract class Logger : IDisposable
    {
        public static readonly Logger Null = new NullLogger();

        public virtual void LogError(string error)
        {
            LogMessage(error);
        }

        public virtual void LogExceptionError(string operation, Exception ex)
        {
            LogError($"Operation: {operation}{Environment.NewLine}{ex.ToString()}");
        }

        public virtual void LogWarning(string warning)
        {
            LogMessage(warning);
        }

        [Conditional("DIAGNOSTIC")]
        public virtual void LogDiagnosticWithProvenance2(string message, [CallerMemberName] string caller = null, [CallerLineNumber] int line = 0) => LogMessage($"{caller}({line}): {message}", MessageKind.Diagnostic);

        [Conditional("DIAGNOSTIC")]
        public virtual void LogDiagnosticWithProvenance(string message, [CallerMemberName] string caller = null, [CallerLineNumber] int line = 0) => LogMessage($"{caller}({line}): {message}", MessageKind.Diagnostic);

        public virtual void LogDiagnostic(string message) => LogMessage(message, MessageKind.Diagnostic);

        public abstract void LogMessage(string message, MessageKind kind = MessageKind.Informational);

        public void WriteLine(string message)
        {
            LogMessage(message);
        }

        public virtual void Dispose()
        {
        }

        public virtual void Flush(bool disableQueuing)
        {

        }

        private class NullLogger : Logger
        {
            public override void LogMessage(string message, MessageKind kind = MessageKind.Informational)
            {
            }
        }
    }

    public enum MessageKind
    {
        Informational,
        Diagnostic,
    }
}
