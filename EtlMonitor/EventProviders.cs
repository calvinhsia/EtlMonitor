using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Performance.ResponseTime
{
    internal static class EventProviders
    {
        public static readonly Guid ResponsivenessProviderGuid = new Guid("4E17E413-3C0C-4C2A-A531-C1799A05AD7C");
        public static readonly Guid VsFingerprintProviderGuid = new Guid("8D99DCFC-34B7-407B-BB6C-F81FE22CB5D3");
        // this is a private guid for VS related ETW events.
        public static readonly Guid VisualStudioCodeMarkersProviderGuid = new Guid("641d7f6c-481c-42e8-ab7e-d18dc5e5cb9e");
        // this is the public runtime clr guid.
        // it's also the same guid used in the TraceEvent Library for ClrTraceEventParser
        public static readonly Guid ClrProviderGuid = new Guid(unchecked((int)0xe13c0d23), unchecked((short)0xccbc), unchecked((short)0x4e12), 0x93, 0x1b, 0xd9, 0xcc, 0x2e, 0xee, 0x27, 0xe4);
        // This is the GUID for the rundown provider. If module/method/etc. rundown is desired, this is the provider that needs to be used.
        public static readonly Guid ClrRundownProviderGuid = new Guid("A669021C-C450-4609-A035-5AF59AF4DF18");
        // this is a private guid for VS related ETW Events.
        public static readonly Guid MeasurementBlockProviderGuid = new Guid("143a31db-0372-40b6-b8f1-b4b16adb5f54");
        public static readonly Guid RetailAssertProviderGuid = new Guid("EE328C6F-4C94-45F7-ACAF-640C6A447654");

        public static readonly Guid VsThreadingProvider = new Guid("589491ba-4f15-53fe-c376-db7f020f5204");

        public const long AllKeywords = 0;

        public enum ResponsivenessEventIds : ushort
        {
            Mark = 1,
            ProviderStart = 2,
            ProviderEnd = 3,
            UnresponsivenessStart = 4,
            UnresponsivenessEnd = 5,
            PotentialDeadlockDetected = 6,
            UnresponsiveStackTrace = 100,
            NativeModuleLoad = 202,
            NativeModuleUnload = 203,
            NativeModuleLoadRundown = 204,

            WindowMessageProcessStart = 500,
            WindowMessageProcessStop = 501,
            UnresponsiveWindowMessage = 502,

            ContextSolutionOpen = 600,
            ContextSolutionClosing = 601,
            ContextToolWindowSelectedStart = 602,
            ContextToolWindowSelectedStop = 603,
            ContextDocumentWindowSelectedStart = 604,
            ContextDocumentWindowSelectedStop = 605,

            ExtensionRunDownStart = 620,
            ExtensionRunDownStop = 621,
            ExtensionRunDownItem = 622,
            ExtensionAssetRunDownItem = 623,

            UserConfiguration = 630,

            VSSessionId = 640,  // sqm VS Instance Id

            VSLocalRegRoot = 644,  // VS Local Reg Root so we can lookup PW settings in same place

            VSTelemetrySettings = 645,  // AppInsights session settings

            TelemetryCostStart = 700,
            TelemetryCostStop = 701,

            Diagnostic = 800,

            LightWeightTelemetryEvent = 900
        }

        public enum ResponsivenessProviderKeywords : ulong
        {
            Tracking = 0x1,
            Stacks = 0x2,
            Module = 0x4,
            UnresponsiveWindowMessage = 0x8,
            WindowMessage = 0x10,
            VisualStudioContext = 0x20,
            TelemetryCost = 0x40,
            Diagnositic = 0x80,
            VsFingerprint = 0x100,
            LightWeightTelemetry = 0x200,
            Default =
                Tracking | Stacks | Module | UnresponsiveWindowMessage
                | VisualStudioContext | TelemetryCost | Diagnositic | VsFingerprint
                | LightWeightTelemetry
        }

        public enum ClrProviderKeywords : ulong
        {
            /// <summary>
            /// Logging when garbage collections and finalization happen. 
            /// </summary>
            GC = 0x1,
            Binder = 0x4,
            /// <summary>
            /// Logging when modules actually get loaded and unloaded. 
            /// </summary>
            Loader = 0x8,
            /// <summary>
            /// Logging when Just in time (JIT) compilation occurs. 
            /// </summary>
            Jit = 0x10,
            /// <summary>
            /// Logging when precompiled native (NGEN) images are loaded.
            /// </summary>
            NGen = 0x20,
            /// <summary>
            /// Indicates that on attach or module load , a rundown of all existing methods should be done
            /// </summary>
            StartEnumeration = 0x40,
            /// <summary>
            /// Indicates that on detach or process shutdown, a rundown of all existing methods should be done
            /// </summary>
            StopEnumeration = 0x80,
            /// <summary>
            /// Events associted with validating security restrictions.
            /// </summary>
            Security = 0x400,
            /// <summary>
            /// Events for logging resource consumption on an app-domain level granularity
            /// </summary>
            AppDomainResourceManagement = 0x800,
            /// <summary>
            /// Logging of the internal workings of the Just In Time compiler.  This is fairly verbose.  
            /// It details decidions about interesting optimization (like inlining and tail call) 
            /// </summary>
            JitTracing = 0x1000,
            /// <summary>
            /// Log information about code thunks that transition between managed and unmanaged code. 
            /// </summary>
            Interop = 0x2000,
            /// <summary>
            /// Log when lock conentions occurs.  (Monitor.Enters actually blocks)
            /// </summary>
            Contention = 0x4000,
            /// <summary>
            /// Log exception processing.  
            /// </summary>
            Exception = 0x8000,
            /// <summary>
            /// Log events associated with the threadpool, and other threading events.  
            /// </summary>
            Threading = 0x10000,
            /// <summary>
            /// Also log the stack trace of events for which this is valuable.
            /// </summary>
            Stack = 0x40000000
        }

        public enum ClrProviderEventIds : ushort
        {
            GCStart = 1,
            GCStop = 2,
            GCHeapStats = 4,
            GCCreateSegment = 5,
            GCFreeSegment = 6,
            GCRestartEEBegin = 7,
            GCRestartEEEnd = 3,
            GCSuspendEEEnd = 8,
            GCSuspendEEBegin = 9,
            GCAllocationTick = 10,

            ExceptionStart = 80,
            MethodLoad = 141,
            MethodUnload = 142,
            MethodLoadVerbose = 143,
            MethodUnloadVerbose = 144,
            MethodJittingStarted = 145,
            ModuleLoad = 152,
            ModuleUnload = 153,
            CLRInstanceLoad = 187,
            ExceptionCatchStart = 250,
            ExceptionCatchStop = 251,
            ExceptionFinallyStart = 252,
            ExceptionFinallyStop = 253,
            ExceptionStop = 256
        }

        public enum GCSuspendEEReason
        {
            SuspendOther = 0x0,
            SuspendForGC = 0x1,
            SuspendForAppDomainShutdown = 0x2,
            SuspendForCodePitching = 0x3,
            SuspendForShutdown = 0x4,
            SuspendForDebugger = 0x5,
            SuspendForGCPrep = 0x6,
            SuspendForDebuggerSweep = 0x7,
        }

        public enum GCSegmentType
        {
            SmallObjectHeap = 0x0,
            LargeObjectHeap = 0x1,
            ReadOnlyHeap = 0x2,
        }
        public enum GCAllocationKind
        {
            Small = 0x0,
            Large = 0x1,
        }
        public enum GCType
        {
            NonConcurrentGC = 0x0,      // A 'blocking' GC.  
            BackgroundGC = 0x1,         // A Gen 2 GC happening while code continues to run
            ForegroundGC = 0x2,         // A Gen 0 or Gen 1 blocking GC which is happening when a Background GC is in progress.  
        }
        public enum GCReason
        {
            AllocSmall = 0x0,
            Induced = 0x1,
            LowMemory = 0x2,
            Empty = 0x3,
            AllocLarge = 0x4,
            OutOfSpaceSOH = 0x5,
            OutOfSpaceLOH = 0x6,
            InducedNotForced = 0x7,
            Internal = 0x8,
            InducedLowMemory = 0x9,
        }

        public enum ClrRundownProviderEventIds : ushort
        {
            MethodDCStart = 141,
            MethodDCEnd = 142,
            MethodDCStartVerbose = 143,
            MethodDCEndVerbose = 144,
            ModuleDCStart = 153,
            ModuleDCEnd = 154
        }

        public enum CodeMarkerProviderEventIds : ushort
        {
            ModalDialogBegin = 512,
            PerfVSFinsihedBooting = 7103, // end of startup
            PerfScenarioStop = 7491,      // end of startup based on diagnostic scenario provider
            InputDelay = 9445,                          // perfVSInputDelay RaiseInputDelayMarker  env\msenv\core\main.cpp
            ShellUIActiveViewSwitchEnd = 18116,         // perfShellUI_ActiveViewSwitchEnd  env\shell\viewmanager\viewmanager.cs
            PerfWatsonHangDumpCollected = 18712,        // Indicates that a hang dump was collected in the VS process, and has the watson report id as the payload.
            perfTargetedTraceBegin = 18987,
            perfTargetedTraceEnd = 18988,
            perfTargetedTraceAbandon = 18989,
            perfDelayAttributionSet = 7493,
            perfDelayAttributionClear = 7494,
            perfDelayAttributionReset = 7495
        }

        public enum MicrosoftThreadingProviderEventIds : ushort
        {
            CompleteOnCurrentThreadStart = 11,
            CompleteOnCurrentThreadEnd = 12,
            SwitchToMainThreadRequested = 15,
            SwitchToMainThreadCompleted = 16
        }

        public enum RetailAssertKeywords : ushort
        {
            xxxUnUsedxxxAssert = 0x4,
            VirtualAllocs = 0x8,
        }


        public class DiagnosticsScenarioKeys
        {
            public const string ScenarioKey_Startup = "Startup";
        }
    }
}
