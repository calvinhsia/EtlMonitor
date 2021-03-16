using EtlMonitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace Tests
{
    [TestClass]
    public class UnitTest1 : MyTestBase
    {
        [TestMethod]
        public void TestMethod1()
        {
            Trace.WriteLine("test");
            VerifyLogStrings("test");
        }
    }
}
