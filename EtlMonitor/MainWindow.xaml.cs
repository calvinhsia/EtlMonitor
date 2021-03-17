using Microsoft.Performance.ResponseTime;
using Microsoft.VisualStudio.Telemetry.ETW;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Utility;

namespace EtlMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MyTraceListener MyTraceListener;
        public bool IsFilterToPid { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.WindowState = WindowState.Maximized;
            this.MyTraceListener = new MyTraceListener(options: MyTraceListener.MyTextWriterTraceListenerOptions.AddDateTime);
            this.MyTraceListener.MyTraceListenerOnWriteLine += (o, e) =>
              {
                  _txtStatus.Dispatcher.BeginInvoke(
                      new Action(() =>
                      {
                          _txtStatus.AppendText(e.MessageLine + Environment.NewLine);
                          _txtStatus.ScrollToEnd();
                      }));
              };
            this.Loaded += MainWindow_Loaded;
            this.Closed += (o, e) =>
              {
                  MyTraceListener.Dispose();
              };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var isTracing = false;
                var eventReceiver = new EventReceiver();
                RealtimeETWListener realtimeETWListener = null;
                this.btnGo.Click += async (o, eb) =>
                {
                    isTracing = !isTracing;
                    try
                    {
                        if (isTracing)
                        {
                            btnGo.Content = "_Stop";
                            await Task.Run(() =>
                            {
                                realtimeETWListener = new RealtimeETWListener("MyListener", eventReceiver);
                                Process ProcToFilterTo = null;
                                if (IsFilterToPid)
                                {
                                    ProcToFilterTo = Process.GetProcessesByName("devenv").First();
                                }
                                Trace.WriteLine($"Filtering to Pid {ProcToFilterTo?.Id} {ProcToFilterTo?.MainWindowTitle}");

                                realtimeETWListener.EnableProvider(
                                    EventProviders.ClrProviderGuid,
                                    level: 4,
                                    matchAnyKeyword: (ulong)EventProviders.ClrProviderKeywords.GC,
                                    matchAllKeywords: 0,
                                    enableStacks: true,
                                    PidToFilter: ProcToFilterTo == null ? 0 : ProcToFilterTo.Id
                                    );

                                realtimeETWListener.Begin();

                            });
                        }
                        else
                        {
                            Trace.WriteLine("Stopping Tracing");
                            btnGo.IsEnabled = false;
                            btnGo.Content = "_Go";
                            await Task.Delay(TimeSpan.FromMilliseconds(10));
                            realtimeETWListener.End();
                            Trace.WriteLine("Tracing stopped");
                            btnGo.IsEnabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.ToString());
                    }
                };
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }
    }
    internal class EventReceiver : IEventRecordReceiver
    {
        public List<EventData> lstEvents = new List<EventData>();
        public void ReceiveEvent(EventData eventData)
        {
            lstEvents.Add(eventData.Clone()); // to persist, must make a copy so it doesn't get overwritten by next event
            var proc = Process.GetProcessById(eventData.ProcessId);
            var id = (EventProviders.ClrProviderEventIds)eventData.Id;
            Trace.WriteLine($"Received event {proc.ProcessName} {id} {eventData}");
        }
    }
}
