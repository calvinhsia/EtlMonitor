using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utility
{
    public class MyTraceListenerWriteLineArgs : EventArgs
    {
        public string MessageLine { get; private set; }
        public MyTraceListenerWriteLineArgs(string line)
        {
            this.MessageLine = line;
        }
    }
    public class MyTraceListener : TextWriterTraceListener, IDisposable
    {
        private readonly string LogFileName;
        private readonly MyTextWriterTraceListenerOptions options;
        public List<string> _lstLoggedStrings;

        public event EventHandler<MyTraceListenerWriteLineArgs> MyTraceListenerOnWriteLine;

        readonly ConcurrentQueue<string> _qLogStrings;

        private Task taskOutput;
        private CancellationTokenSource ctsBatchProcessor;

        [Flags]
        public enum MyTextWriterTraceListenerOptions
        {
            None = 0x0,
            AddDateTime = 0x1,
            /// <summary>
            /// Some tests take a long time and if they fail, it's difficult to examine any output
            /// Also, getting the test output is very cumbersome: need to click on the Additional Output, then right click/Copy All output, then paste it somewhere
            /// Plus there's a bug that the right-click/copy all didn't copy all in many versions.
            /// Turn this on to output to a file in real time. Open the file in VS to watch the test progress
            /// </summary>
            OutputToFile = 0x2,
            /// <summary>
            /// Output to file while running takes a long time. Do it in batches every second. Ensure disposed
            /// so much faster (20000 lines takes minutes vs <500 ms)
            /// </summary>
            OutputToFileAsync = 0x4
        }

        public MyTraceListener(
            string LogFileName = null, 
            MyTextWriterTraceListenerOptions options = MyTextWriterTraceListenerOptions.OutputToFileAsync | MyTextWriterTraceListenerOptions.AddDateTime)
        {
            if (string.IsNullOrEmpty(LogFileName))
            {
                LogFileName = System.Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Desktop\TestOutput.txt");
            }
            this.LogFileName = LogFileName;
            this.options = options;
            _lstLoggedStrings = new List<string>();
            MyTraceListenerOnWriteLine += (o, e) =>
              {
                  _lstLoggedStrings.Add(e.MessageLine);
              };
            Trace.Listeners.Clear(); // else Debug.Writeline can cause infinite recursion because the Test runner adds a listener.
            Trace.Listeners.Add(this);
            if (options.HasFlag(MyTextWriterTraceListenerOptions.OutputToFile) || options.HasFlag(MyTextWriterTraceListenerOptions.OutputToFileAsync))
            {
                File.Delete(LogFileName);
                if (this.options.HasFlag(MyTextWriterTraceListenerOptions.OutputToFileAsync))
                {
                    _qLogStrings = new ConcurrentQueue<string>();
                    MyTraceListenerOnWriteLine += (o, e) =>
                      {
                          _qLogStrings.Enqueue(e.MessageLine);
                      };
                    this.taskOutput = Task.Run(async () =>
                    {
                        try
                        {
                            this.ctsBatchProcessor = new CancellationTokenSource();
                            bool fShutdown = false;
                            while (!fShutdown)
                            {
                                if (this.ctsBatchProcessor.IsCancellationRequested)
                                {
                                    fShutdown = true; // we need to go through one last time to clean up
                                }
                                var lstBatch = new List<string>();
                                while (!_qLogStrings.IsEmpty)
                                {
                                    if (_qLogStrings.TryDequeue(out var msg))
                                    {
                                        lstBatch.Add(msg);
                                    }
                                }
                                if (lstBatch.Count > 0)
                                {
                                    await MyTraceListener.OutputToLogFileWithRetryAsync(() =>
                                    {
                                        File.AppendAllLines(LogFileName, lstBatch);
                                    });
                                }
                                try
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(1), this.ctsBatchProcessor.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    });
                }
            }
            else
            {
                MyTraceListenerOnWriteLine += (o, e) =>
                      {
                          try
                          {
                              if (this.taskOutput != null)
                              {
                                  this.taskOutput.Wait();
                              }
                              if (!this.taskOutput.IsCompleted) // faulted?
                              {
                                  Trace.WriteLine(("Output Task faulted"));
                              }
                              this.taskOutput = Task.Run(async () =>
                              {
                                  await OutputToLogFileWithRetryAsync(() =>
                                  {
                                      File.AppendAllText(LogFileName, e.MessageLine + Environment.NewLine);
                                  });
                              });
                          }
                          catch (OperationCanceledException)
                          {
                          }
                      };
            }
        }

        public override void Write(object o)
        {
            Write(o.ToString());
        }
        public override void Write(string message)
        {
            var dt = string.Empty;
            if (this.options.HasFlag(MyTextWriterTraceListenerOptions.AddDateTime))
            {
                dt = string.Format("[{0}],",
                    DateTime.Now.ToString("hh:mm:ss:fff")
                    ) + $"{Thread.CurrentThread.ManagedThreadId,2} ";
            }
            message = dt + message.Replace("{", "{{").Replace("}", "}}");
            MyTraceListenerOnWriteLine(this, new MyTraceListenerWriteLineArgs(message));
        }

        public static async Task OutputToLogFileWithRetryAsync(Action actWrite)
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
                await Task.Delay(TimeSpan.FromSeconds(0.3));
            }
            if (!success)
            {
                throw new Exception($"Error writing to log #retries ={nRetry}");
            }
        }

        public override void WriteLine(object o)
        {
            Write(o.ToString());
        }
        public override void WriteLine(string message)
        {
            Write(message);
        }
        public void WriteLine(string str, params object[] args)
        {
            Write(string.Format(str, args));
        }
        protected override void Dispose(bool disposing)
        {
            Trace.Listeners.Remove(this);
            if (this.taskOutput != null)
            {
                this.ctsBatchProcessor.Cancel();
                this.taskOutput.Wait();
            }
            base.Dispose(disposing);
        }
    }

}
