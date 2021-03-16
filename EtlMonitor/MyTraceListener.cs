using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EtlMonitor
{
    public class MyTraceListenerWriteLineArgs: EventArgs
    {
        public string Line { get; private set; }
        public MyTraceListenerWriteLineArgs(string line)
        {
            this.Line = line;
        }
    }

    public class MyTraceListener : TextWriterTraceListener
    {
        [Flags]
        public enum MyTraceListenerOptions
        {
            OutputToFileOnDisk = 0x1,

        }
        public event EventHandler<MyTraceListenerWriteLineArgs> MyTraceListenerOnWriteLine;
        public List<string> lstLoggedStrings = new List<string>();
        bool IsInTraceListener = false;

        public MyTraceListener(MyTraceListenerOptions opts = MyTraceListenerOptions.OutputToFileOnDisk)
        {
            if (opts.HasFlag(MyTraceListenerOptions.OutputToFileOnDisk))
            {
                MyTraceListenerOnWriteLine += (o, e) =>
                {
                    this.lstLoggedStrings.Add(e.Line);
                };
            }
            Trace.Listeners.Add(this);
        }
        public override void WriteLine(string str)
        {
            if (!IsInTraceListener)
            {
                IsInTraceListener = true; // prevent recursion (e.g. from Debug.WriteLine)
                var dt = string.Format("[{0}],",
                     DateTime.Now.ToString("hh:mm:ss:fff")
                     ) + $"{Thread.CurrentThread.ManagedThreadId,2} ";
                MyTraceListenerOnWriteLine?.Invoke(this, new MyTraceListenerWriteLineArgs(dt + str));
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine(dt + str);
                }
                IsInTraceListener = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            var leftovers = string.Join("\r\n     ", lstLoggedStrings);

            ForceAddToLog("LeftOverLogs\r\n     " + leftovers + "\r\n");
            Trace.Listeners.Remove(this);
        }

        internal void ForceAddToLog(string str)
        {
            var outfile = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Desktop\TestOutput.txt");
            var leftovers = string.Join("\r\n     ", lstLoggedStrings) + "\r\n" + str;
            lstLoggedStrings.Clear();
            OutputToLogFileWithRetryAsync(() =>
            {
                File.AppendAllText(outfile, str + "\r\n");
            });
        }
        public void OutputToLogFileWithRetryAsync(Action actWrite)
        {
            var nRetry = 0;
            var success = false;
            while (nRetry++ < 10)
            {
                try
                {
                    actWrite();
                    success = true;
                    break;
                }
                catch (IOException)
                {
                }

                Task.Delay(TimeSpan.FromSeconds(0.3)).Wait();
            }
            if (!success)
            {
                Trace.WriteLine($"Error writing to log #retries ={nRetry}");
            }
        }
    }
}
