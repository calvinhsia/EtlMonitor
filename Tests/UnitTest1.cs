using EtlMonitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Telemetry.ETW;
using Microsoft.Performance.ResponseTime;
using System.Threading.Tasks;

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
            await Task.Delay(TimeSpan.FromSeconds(3));
            VerifyLogStrings("test");
            this.EtwListener.End();
        }
    }
    internal class EventReceiver : IEventRecordReceiver
    {
        public void ReceiveEvent(EventData eventData)
        {
            Trace.WriteLine($"Received event{eventData}");
        }
    }
}
