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
            var cnt = await DoItAsync(Filter: false);
            Assert.IsTrue(cnt > 1, "Expecting many managed processes");
        }
        [TestMethod]
        public async Task TestEtwFilter()
        {
            var cnt = await DoItAsync(Filter: true);
            Assert.AreEqual(cnt, 1, $"Expected 1 managed process. Got {cnt}");
        }

        private async Task<int> DoItAsync(bool Filter)
        {
            var eventReceiver = new EventReceiver();
            this.EtwListener = new RealtimeETWListener("MyListener", eventReceiver);
            Process ProcToFilterTo = null;
            if (Filter)
            {
                ProcToFilterTo = Process.GetProcessesByName("devenv").First();
            }
            Trace.WriteLine($"Filtering to Pid {ProcToFilterTo?.Id} {ProcToFilterTo?.MainWindowTitle}");

            this.EtwListener.EnableProvider(
                EventProviders.ClrProviderGuid,
                level: 4,
                matchAnyKeyword: (ulong)EventProviders.ClrProviderKeywords.GC,
                matchAllKeywords: 0,
                enableStacks: true,
                PidToFilter: ProcToFilterTo == null ? 0 : ProcToFilterTo.Id
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
