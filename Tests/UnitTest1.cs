using EtlMonitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.Telemetry.ETW;
using Microsoft.Performance.ResponseTime;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class UnitTest1 : MyTestBase
    {
        private RealtimeETWListener EtwListener;

        [TestMethod]
        public async Task TestEtwNoFilter()
        {
            var settings = new RealtimeETWListener.ProviderSettings()
            {
                Guid = EventProviders.ClrProviderGuid,
                Level = 4,
                MatchAll = 0,
                MatchAny = (ulong)EventProviders.ClrProviderKeywords.GC,
                EnableStacks = true,
                PidToFilter = 0
            };
            var cnt = await DoItAsync(settings);
            Assert.IsTrue(cnt > 1, "Expecting many managed processes");
        }
        [TestMethod]
        public async Task TestEtwFilter()
        {
            var ProcToFilterTo = Process.GetProcessesByName("devenv").First();
            Trace.WriteLine($"Filtering to Pid {ProcToFilterTo?.Id} {ProcToFilterTo?.MainWindowTitle}");
            var settings = new RealtimeETWListener.ProviderSettings()
            {
                Guid = EventProviders.ClrProviderGuid,
                Level = 4,
                MatchAll = 0,
                MatchAny = (ulong)EventProviders.ClrProviderKeywords.GC,
                EnableStacks = true,
                PidToFilter = ProcToFilterTo.Id
            };
            var cnt = await DoItAsync(settings);
            Assert.AreEqual(1, cnt, $"Expected 1 managed process. Got {cnt}");
        }

        [TestMethod]
        public async Task TestEtwFilterRundown()
        {
            var ProcToFilterTo = Process.GetProcessesByName("devenv").First();
            Trace.WriteLine($"Filtering to Pid {ProcToFilterTo?.Id} {ProcToFilterTo?.MainWindowTitle}");
            var settings = new RealtimeETWListener.ProviderSettings()
            {
                Guid = EventProviders.ClrRundownProviderGuid,
                Level = 4,
                MatchAll = 0,
                MatchAny = (ulong)EventProviders.ClrProviderKeywords.Jit
                        | (ulong)EventProviders.ClrProviderKeywords.Loader
                        | (ulong)EventProviders.ClrProviderKeywords.StopEnumeration,
                EnableStacks = true,
                PidToFilter = ProcToFilterTo.Id
            };
            var cnt = await DoItAsync(settings);
            Assert.AreEqual(cnt, 1, $"Expected 1 managed process. Got {cnt}");
        }



        private async Task<int> DoItAsync(RealtimeETWListener.ProviderSettings settings)
        {
            var eventReceiver = new EventReceiver();
            this.EtwListener = new RealtimeETWListener("MyListener", eventReceiver);

            this.EtwListener.EnableProvider(
                settings.Guid,
                level: settings.Level,
                matchAnyKeyword: settings.MatchAny,
                matchAllKeywords: settings.MatchAll,
                enableStacks: settings.EnableStacks,
                PidToFilter: settings.PidToFilter
                );

            this.EtwListener.Begin();
            await Task.Delay(TimeSpan.FromSeconds(10));
            this.EtwListener.End();
            Trace.WriteLine("Tracing stopped");
            var res = from evd in eventReceiver.lstEvents
                      group evd by evd.ProcessId into grp
                      select new
                      {
                          PID = grp.Key,
                          Proc = Process.GetProcessById(grp.Key).ProcessName,
                          Cnt = grp.Count(),
                      };
            foreach (var dat in res)
            {
                Trace.WriteLine($" PID={dat.PID}  #Ev={dat.Cnt}  {dat.Proc}");
            }
            return res.Count();
        }
    }
}
