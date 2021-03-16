
namespace Microsoft.VisualStudio.Telemetry.ETW
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Telemetry.Services;
    using AppInsights = AppInsights::Microsoft.VisualStudio.Telemetry;


    internal class RealtimeETWListener
    {
        private const uint BufferSize = 512;  // 512k buffer  
        private string listenerName; // Microsoft-VisualStudio-Telemetry-PerfWatson2
        private bool isTracing;
        private ulong traceHandle = TraceEventNativeMethods.INVALID_HANDLE_VALUE;
        private Thread processWorkerThread;
        private IEventRecordReceiver receiver;
        private List<ProviderSettings> preStartProviders = new List<ProviderSettings>();
        private TraceEventNativeMethods.EventTraceEventCallback dispatchDelegate;
        private TraceEventNativeMethods.EventTraceBufferCallback bufferDelegate;
        private EventData rawDataSingleton = new EventData();
        private unsafe TraceEventNativeMethods.EVENT_TRACE_PROPERTIES* properties;
        private ulong logHandle;
        private IntPtr propertiesBuffer;
        private readonly TimeSpan FileSizeCheckTimeout = TimeSpan.FromSeconds(180);
        private Timer checkIfUpdateIsDisabled;
        private IServiceProvider serviceProvider;

        public RealtimeETWListener(string listenerName, IEventRecordReceiver receiver)
        {
            Contract.Requires(receiver != null);
            Contract.Requires(!String.IsNullOrEmpty(listenerName));
            this.listenerName = listenerName;
            this.receiver = receiver;

            serviceProvider = receiver as IServiceProvider;
            this.checkIfUpdateIsDisabled = new Timer(
                (e) => UpdateIsCollectionDisabled(),
                null, TimeSpan.FromSeconds(30), FileSizeCheckTimeout);
        }

        public void Begin()
        {
            if (isTracing)
            {
                return;
            }

            SetupTrace();
            BeginProcessTrace();
        }

        private unsafe void EnableProviderInternal(ProviderSettings settings)
        {
            TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS traceParams = new TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS();
            traceParams.Version = TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS_VERSION;
            traceParams.EnableProperty = settings.EnableStacks ? TraceEventNativeMethods.EVENT_ENABLE_PROPERTY_STACK_TRACE : 0;

            int err = TraceEventNativeMethods.EnableTraceEx2(
                this.traceHandle,
                ref settings.Guid,
                TraceEventNativeMethods.EVENT_CONTROL_CODE_ENABLE_PROVIDER,
                (byte)settings.Level,
                settings.MatchAny,
                settings.MatchAll,
                0, ref traceParams);

            if (err != 0)
            {
                throw new Win32Exception(err);
            }
        }

        public void End(bool waitForThreads = true)
        {
            if (!isTracing)
            {
                return;
            }

            unsafe
            {
                LoggerBase.WriteInformation("Stopping Trace Listener");
                // Flush the trace. 
                TraceEventNativeMethods.ControlTrace(this.traceHandle, this.listenerName, this.properties, TraceEventNativeMethods.EVENT_TRACE_CONTROL_FLUSH);

                // Set us as not tracing any more, this will end the processing thread. 
                isTracing = false;

                // if this code is called from the unhandled exception handler than other threads are suspended 
                // or aborted and the Join will deadlock. In this case waitForThreads will be true to prevent
                // deadlocking.  In other cases the join should occur quickly once isProcessing is false, but 
                // the Join will timeout after 60 seconds just in case something's gone wrong to prevent deadlock.
                if (waitForThreads)
                {
                    processWorkerThread.Join(TimeSpan.FromSeconds(60));
                }
                processWorkerThread = null;

                // Stop the trace 
                TraceEventNativeMethods.ControlTrace(this.traceHandle, this.listenerName, this.properties, TraceEventNativeMethods.EVENT_TRACE_CONTROL_STOP);

                // Close the trace 
                TraceEventNativeMethods.CloseTrace(this.traceHandle);

                if (this.propertiesBuffer != IntPtr.Zero)
                {
                    // Delete the properties buffer
                    Marshal.FreeCoTaskMem(this.propertiesBuffer);
                    this.propertiesBuffer = IntPtr.Zero;
                }
            }
        }

        private void BeginProcessTrace()
        {
            processWorkerThread = new Thread(this.ProcessWorker);
            processWorkerThread.Start();
        }

        internal void EnableProvider(Guid providerId, int level, ulong matchAnyKeyword, ulong matchAllKeywords, bool enableStacks)
        {
            ProviderSettings settings = new ProviderSettings()
            {
                Guid = providerId,
                Level = level,
                MatchAny = matchAnyKeyword,
                MatchAll = matchAllKeywords,
                EnableStacks = enableStacks
            };

            if (isTracing)
            {
                this.EnableProviderInternal(settings);
            }
            else
            {
                this.preStartProviders.Add(settings);
            }
        }

        private unsafe void SetupTrace()
        {
            int MaxNameSize = 1024;
            int PropertiesSize = sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES) + 2 * MaxNameSize * sizeof(char);

            this.propertiesBuffer = Marshal.AllocCoTaskMem(PropertiesSize);

            TraceEventNativeMethods.ZeroMemory(propertiesBuffer, (uint)PropertiesSize);

            properties = (TraceEventNativeMethods.EVENT_TRACE_PROPERTIES*)propertiesBuffer;

            properties->LoggerNameOffset = (uint)sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES);
            properties->LogFileNameOffset = 0;

            char* sessionNamePtr = (char*)(((byte*)properties) + properties->LoggerNameOffset);
            TraceEventNativeMethods.CopyStringToPtr(sessionNamePtr, this.listenerName);

            properties->Wnode.BufferSize = (uint)PropertiesSize;
            properties->Wnode.Flags = TraceEventNativeMethods.WNODE_FLAG_TRACED_GUID;
            properties->FlushTimer = 1;              // flush every second;
            properties->BufferSize = BufferSize;
            properties->LogFileMode = TraceEventNativeMethods.EVENT_TRACE_REAL_TIME_MODE;
            properties->MinimumBuffers = (uint)40;
            properties->MaximumBuffers = (uint)(40 * 4);
            properties->Wnode.ClientContext = 1;    // set Timer resolution to QPC. 

            TraceEventNativeMethods.SetSystemProfilePrivilege();

            dispatchDelegate = new TraceEventNativeMethods.EventTraceEventCallback(this.RawDispatch);
            bufferDelegate = new TraceEventNativeMethods.EventTraceBufferCallback(this.TraceEventBufferCallback);

            var logFile = new TraceEventNativeMethods.EVENT_TRACE_LOGFILEW();
            logFile.LoggerName = listenerName;
            logFile.LogFileMode |= TraceEventNativeMethods.EVENT_TRACE_REAL_TIME_MODE;
            logFile.BufferCallback = bufferDelegate;
            logFile.LogFileMode |= TraceEventNativeMethods.PROCESS_TRACE_MODE_EVENT_RECORD;
            logFile.EventCallback = dispatchDelegate;
            logFile.LogFileMode |= TraceEventNativeMethods.PROCESS_TRACE_MODE_RAW_TIMESTAMP;

            isTracing = true;
            LoggerBase.WriteDebug("{0}", listenerName);

            int err = TraceEventNativeMethods.StartTrace(out this.traceHandle, this.listenerName, properties);

            if (err != 0)
            {
                if (err == 183)
                {
                    // File already exists, this is okay, let's shutdown the old trace and try again. 
                    err = TraceEventNativeMethods.ControlTrace(0, this.listenerName, properties, (uint)TraceEventNativeMethods.EVENT_TRACE_CONTROL_STOP);

                    if (err != 4201 && err != 0)
                    {
                        throw new Win32Exception(err, string.Format("{0}", listenerName));
                    }

                    // Try again 
                    err = TraceEventNativeMethods.StartTrace(out this.traceHandle, this.listenerName, properties);
                }
                if (err == 1450) //Insufficient system resources exist to complete the requested service. 
                {
                    throw new Win32Exception(err, string.Format("Insufficient system resources exist to complete the requested service. {0}", listenerName));
                }

                if (err != 0)
                {
                    throw new Win32Exception(err, string.Format("{0}", listenerName));
                }
            }

            this.logHandle = TraceEventNativeMethods.OpenTrace(ref logFile);

            foreach (var item in this.preStartProviders)
            {
                this.EnableProviderInternal(item);
            }

            this.preStartProviders.Clear();
        }

        private void ProcessWorker()
        {
            LoggerBase.WriteDebug("Begin processing trace");
            // This is a blocking call. It will end when the trace is closed or 
            // TraceEventBufferCallback returns false. 
            int error = TraceEventNativeMethods.ProcessTrace(new ulong[] { this.logHandle }, 1, IntPtr.Zero, IntPtr.Zero);

            if (error != 0)
            {
                throw new Win32Exception(error);
            }

            // check if the VS session is still active which would indicate the ETL session was terminated abnormally
            var telemetryService = this.serviceProvider.GetService<TelemetryService>();
            var processService = this.serviceProvider.GetService<ProcessService>();
            if (processService != null
                && telemetryService != null
                && telemetryService.Targets.Any())
            {
                var telemetrySession = telemetryService.Targets.First();
                if (processService.IsProcessRunning(telemetrySession.RootProcess.ProcessId))
                {
                    telemetrySession.WasAbnormalTermination = true;
                    var etlExited = new AppInsights.TelemetryEvent("VS/PerfWatson2/ETLSessionAbnormalShutdown");
                    etlExited.Properties["VS.PerfWatson2.FailedProcessName"] = telemetrySession.RootProcess.ProcessName;
                    AISessionHelper.PostEvent(etlExited);
                }
            }
        }

        [AllowReversePInvokeCalls]
        private bool TraceEventBufferCallback(IntPtr rawLogFile)
        {
            return isTracing;
        }

        private void UpdateIsCollectionDisabled()
        {
            if (this.serviceProvider != null)
            {
                ITelemetryCollectionPolicy telemetryCollectionPolicy = this.serviceProvider.GetService<ITelemetryCollectionPolicy>();
                if (telemetryCollectionPolicy != null && this.isTracing)
                {
                    if (telemetryCollectionPolicy.ShouldStopCollection)
                    {
                        LoggerBase.WriteInformation("Stopping Collection");
                        checkIfUpdateIsDisabled.Change(Timeout.Infinite, Timeout.Infinite);
                        this.End();

                        // downcast to see if we have concrete types needed to log reason for disabling collection
                        var telemetryCollectionPolicyImpl = telemetryCollectionPolicy as TelemetryCollectionPolicy;
                        var telemetryService = serviceProvider as TelemetryService;
                        if (telemetryCollectionPolicyImpl != null && telemetryService != null
                            && telemetryService.Targets != null && telemetryService.Targets.Any())
                        {
                            var defaultSession = telemetryService.Targets.First();
                            var context = defaultSession.GetService<SessionContextService>();
                            if (context != null && !context.Disposed)
                            {
                                if (telemetryCollectionPolicyImpl.IsDebuggerAttached ?? false)
                                {
                                    context.SetSystemValue("IsDebuggerAttached", "true");
                                }

                                if (telemetryCollectionPolicyImpl.HasReachedMaximumTelemetryDatabaseSize ?? false)
                                {
                                    context.SetSystemValue("IsMaxFileSize", "true");
                                }
                            }
                        }
                    }
                }
            }
        }

        [AllowReversePInvokeCalls]
        private unsafe void RawDispatch(TraceEventNativeMethods.EVENT_RECORD* rawData)
        {
            // Call back for real time events, stuff the data into a singleton and delegate
            // this to the event receiver. If the receiver wants to do anything useful, 
            // it will need to copy the data and process it on a background thread. 
            rawDataSingleton.record = rawData;
            rawDataSingleton.userData = rawData->UserData;
            this.receiver.ReceiveEvent(rawDataSingleton);
        }

        private class ProviderSettings
        {
            public Guid Guid;
            public int Level;
            public ulong MatchAny;
            public ulong MatchAll;
            public bool EnableStacks;
        }
    }
}
