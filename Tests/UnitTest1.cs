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
        public async Task TestMethod1()
        {
            Trace.WriteLine("test");
            var eventReceiver = new EventReceiver();
            this.EtwListener = new RealtimeETWListener("MyListener", eventReceiver);
            //// listen to GC etw provider
            //service.AddRequiredEvent(new RequiredEventDescriptor(EventProviders.ClrProviderGuid,
            //    (int)TelemetryService.TraceEventLevel.Informational, (int)(EventProviders.ClrProviderKeywords.GC), enableStacks: false));
            //var d = new RequiredEventDescriptor()
            //{
            //    ProviderId = EventProviders.ClrProviderGuid,
            //    Level = 4,
            //    Keywords = EventProviders.ClrProviderKeywords.GC,
            //    EnableStacks=true
            //};

            this.EtwListener.EnableProvider(
                EventProviders.ClrProviderGuid, 
                level: 4, 
                matchAnyKeyword: (ulong)EventProviders.ClrProviderKeywords.GC,
                matchAllKeywords:0,
                enableStacks: true);

            this.EtwListener.Begin();
            await Task.Delay(TimeSpan.FromSeconds(5));
            this.EtwListener.End();
            Trace.WriteLine("Tracing stopped");
            var res = from evd in eventReceiver.lstEvents
                    group evd by evd.ProcessId into grp
                    select new
                    {
                        PID = grp.Key,
                        Proc = Process.GetProcessById(grp.Key).ProcessName,
                        Cnt=grp.Count(),
                    };
            foreach (var dat in res)
            {
                Trace.WriteLine($" {dat.PID} {dat.Cnt}  {dat.Proc}");
            }
            VerifyLogStrings("test");
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
