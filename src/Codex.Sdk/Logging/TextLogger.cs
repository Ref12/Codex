using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public class TextLogger : Logger
    {
        public readonly TextWriter Writer;
        private readonly bool leaveOpen;
        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private int reservation;
        private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        private bool disableQueuing = false;

        public TextLogger(TextWriter writer, bool leaveOpen = false)
        {
            this.Writer = writer;
            this.leaveOpen = leaveOpen;
        }

        public override void LogError(string error)
        {
            WriteLineCore($"ERROR: {error}");
        }

        public override void LogWarning(string warning)
        {
            WriteLineCore($"WARNING: {warning}");
        }

        public override void LogMessage(string message, MessageKind kind = MessageKind.Informational)
        {
            WriteLineCore(message);
        }

        protected virtual void WriteLineCore(string text)
        {
            text = $"[{stopwatch.Elapsed.ToString(@"hh\:mm\:ss")}]: {text}";

            using (_rwLock.AcquireReadLock())
            {
                if (disableQueuing)
                {
                    Writer.WriteLine(text);
                }
                else
                {
                    messages.Enqueue(text);
                }

                FlushMessages();
            }
        }

        public override void Flush(bool disableQueuing)
        {
            using (_rwLock.AcquireWriteLock())
            {
                FlushMessages();
            }
        }

        private void FlushMessages(bool disableQueuing = false)
        {
            if (Interlocked.CompareExchange(ref reservation, 1, 0) == 0)
            {
                try
                {
                    while (messages.TryDequeue(out var m))
                    {
                        Writer.WriteLine(m);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error writing message. {ex}");
                    Placeholder.LaunchDebugger();
                }
                finally
                {
                    Volatile.Write(ref reservation, 0);
                }
            }
        }

        public override void Dispose()
        {
            Flush(disableQueuing: true);

            if (!leaveOpen)
            {
                Writer.Dispose();
            }
        }
    }
}
