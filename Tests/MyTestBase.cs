using EtlMonitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    public class MyTestBase
    {
        private MyTraceListener MyTraceListener;

        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void TestInitialize()
        {
            this.MyTraceListener = new MyTraceListener();
        }
        [TestCleanup]
        public void TestCleanup()
        {
            this.MyTraceListener.Dispose();
        }
    }
}