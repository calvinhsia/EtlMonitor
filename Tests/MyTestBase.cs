using EtlMonitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Utility;

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
            this.MyTraceListener.MyTraceListenerOnWriteLine += (o, e) =>
              {
                  TestContext.WriteLine(e.MessageLine);
              };
        }
        [TestCleanup]
        public void TestCleanup()
        {
            this.MyTraceListener.Dispose();
        }
        public int VerifyLogStrings(IEnumerable<string> strsExpected, bool ignoreCase = false)
        {
            int numFailures = 0;
            var firstFailure = string.Empty;
            bool IsIt(string strExpected, string strActual)
            {
                var hasit = false;
                if (!string.IsNullOrEmpty(strActual))
                {
                    if (ignoreCase)
                    {
                        hasit = strActual.ToLower().Contains(strExpected.ToLower());
                    }
                    else
                    {
                        hasit = strActual.Contains(strExpected);
                    }
                }
                return hasit;
            }
            foreach (var str in strsExpected)
            {
                if (!MyTraceListener._lstLoggedStrings.Where(s => IsIt(str, s)).Any())
                {
                    numFailures++;
                    if (string.IsNullOrEmpty(firstFailure))
                    {
                        firstFailure = str;
                    }
                    Trace.WriteLine($"Expected '{str}'");
                }
            }
            Assert.AreEqual(0, numFailures, $"1st failure= '{firstFailure}'");
            return numFailures;
        }

        public int VerifyLogStrings(string strings, bool ignoreCase = false)
        {
            var strs = strings.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return VerifyLogStrings(strs, ignoreCase);
        }
    }
}