
namespace Microsoft.VisualStudio.Telemetry.ETW
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using static Microsoft.VisualStudio.Telemetry.ETW.TraceEventNativeMethods;

    //x    using Microsoft.VisualStudio.Telemetry.Services;
    //x    using AppInsights = AppInsights::Microsoft.VisualStudio.Telemetry;
    public struct RequiredEventDescriptor
    {
        public RequiredEventDescriptor(Guid provider, int level, ulong keywords, bool enableStacks = false)
        {
            this.ProviderId = provider;
            this.Level = level;
            this.Keywords = keywords;
            this.EnableStacks = enableStacks;
        }

        public Guid ProviderId;
        public int Level;
        public ulong Keywords;
        public bool EnableStacks;

        public override string ToString()
        {
            return $"{ProviderId} Lev={Level} Kwds={Keywords:x16}, Stks={EnableStacks}";
        }
    }

    internal interface IEventRecordReceiver
    {
        void ReceiveEvent(EventData eventData);
    }
    public interface IEventData
    {
        /// <summary>
        /// Gets the activity identifier for the event. 
        /// </summary>
        Guid ActivityId { get; }

        /// <summary>
        /// Gets the event id. 
        /// </summary>
        ushort Id { get; }

        /// <summary>
        /// Gets the event op code. 
        /// </summary>
        ushort OpCode { get; }

        /// <summary>
        /// Gets the event version
        /// </summary>
        ushort Version { get; }

        /// <summary>
        /// Gets the event process identifier. 
        /// </summary>
        int ProcessId { get; }

        /// <summary>
        /// Gets the event thread identifier.
        /// </summary>
        int ThreadId { get; }

        /// <summary>
        /// Gets the source provider identity.
        /// </summary>
        Guid ProviderId { get; }

        /// <summary>
        /// Gets the timestamp of the event.  Units are in QPC.
        /// </summary>
        long TimeStamp { get; }

        /// <summary>
        /// Gets the user data for the event. 
        /// </summary>
        IEventUserData UserData { get; }

        bool HasUserData { get; }

        /// <summary>
        /// Gets the callstack from extended data if available
        /// </summary>
        /// <remarks>If data is not available method will return null</remarks>
        ulong[] GetCallstackExtendedData();
    }
    internal class EventData : IEventData, IEventUserData
    {
        internal IntPtr cloneBuffer;

        // This is essentialy the bridge to the NativeCode backing the Event.
        // It points to a blob of data, and you can use it to fill out
        // stack traces, exception messages, and other things. 
        internal unsafe TraceEventNativeMethods.EVENT_RECORD* record;

        internal IntPtr userData;

        internal EventData()
        {
        }

        private ulong[] callstackFramesExtendedData;

        ~EventData()
        {
            if (cloneBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(cloneBuffer);
            }
        }

        public Guid ActivityId
        {
            get
            {
                unsafe
                {
                    return record->EventHeader.ActivityId;
                }
            }
        }

        public bool HasUserData
        {
            get
            {
                return this.UserDataLength > 0;
            }
        }

        public ushort Id
        {
            get
            {
                unsafe
                {
                    return record->EventHeader.Id;
                }
            }
        }

        public ushort OpCode
        {
            get
            {
                unsafe
                {
                    return record->EventHeader.Opcode;
                }
            }
        }

        public ushort Version
        {
            get
            {
                unsafe
                {
                    return record->EventHeader.Version;
                }
            }
        }

        public int ProcessId
        {
            get
            {
                unsafe
                {
                    return record->EventHeader.ProcessId;
                }
            }
        }

        public int ThreadId
        {
            get
            {
                unsafe
                {
                    return record->EventHeader.ThreadId;
                }
            }
        }

        public Guid ProviderId
        {
            get
            {
                unsafe
                {
                    return record->EventHeader.ProviderId;
                }
            }
        }

        public long TimeStamp
        {
            get
            {
                unsafe
                {
                    return this.record->EventHeader.TimeStamp;
                }
            }
        }

        public IEventUserData UserData
        {
            get { return this; }
        }

        public ushort UserDataLength
        {
            get
            {
                unsafe
                {
                    return record->UserDataLength;
                }
            }
        }

        public int ExtendedDataCount
        {
            get { unsafe { return record->ExtendedDataCount; } }
        }

        /// <summary>
        /// Goes through the ulong stack addresses at 
        /// <see cref="record->ExtendedData"/> and aggregates them into
        /// a ulong[] list representing the stack of this Event.
        /// </summary>
        /// <returns> a ulong[] representing the stack of this Event.  It's
        /// in reverse order though.</returns>
        public unsafe ulong[] GetCallstackExtendedData()
        {
            if (cloneBuffer != IntPtr.Zero)
            {
                return this.callstackFramesExtendedData;
            }
            else
            {
                var extendedData = record->ExtendedData;
                for (int i = 0; i < record->ExtendedDataCount; i++)
                {
                    if (extendedData[i].ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE64)
                    {

                        var data = (TraceEventNativeMethods.EVENT_EXTENDED_ITEM_STACK_TRACE64*)(extendedData[i].DataPtr);
                        int length = (extendedData[i].DataSize - sizeof(ulong)) / sizeof(ulong);
                        ulong[] frames = new ulong[length];

                        // copy the ulong address in at the offset of data
                        // to frames.
                        for (int x = 0; x < length; x++)
                        {
                            frames[x] = data->Address[x];
                        }
                        return frames;

                    }
                    else if (extendedData[i].ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE32)
                    {
                        var data = (TraceEventNativeMethods.EVENT_EXTENDED_ITEM_STACK_TRACE32*)(extendedData[i].DataPtr);
                        int length = (extendedData[i].DataSize - sizeof(uint)) / sizeof(uint);
                        ulong[] frames = new ulong[length];

                        for (int x = 0; x < length; x++)
                        {
                            frames[x] = data->Address[x];
                        }
                        return frames;
                    }
                }
            }

            return null;
        }


        public EventData Clone()
        {
            return InternalClone();
        }

        public int GetStringEndOffset(int offset)
        {
            IntPtr data = this.userData;

            while (TraceEventRawReaders.ReadInt16(data, offset) != 0)
            {
                offset += 2;
            }

            offset += 2;
            return offset;
        }

        public byte ReadByte(int offset)
        {
            return TraceEventRawReaders.ReadByte(this.userData, offset);
        }

        public byte[] ReadBytes(int offset, ushort length)
        {
            return TraceEventRawReaders.ReadBytes(this.userData, offset, length);
        }

        public Guid ReadGuid(int offset)
        {
            return TraceEventRawReaders.ReadGuid(this.userData, offset);
        }

        public int ReadInt(int offset)
        {
            return TraceEventRawReaders.ReadInt32(this.userData, offset);
        }

        public IntPtr ReadIntPtr(int offset)
        {
            return TraceEventRawReaders.ReadIntPtr(this.userData, offset);
        }

        public long ReadLong(int offset)
        {
            return TraceEventRawReaders.ReadInt64(this.userData, offset);
        }

        public short ReadShort(int offset)
        {
            return TraceEventRawReaders.ReadInt16(this.userData, offset);
        }

        public string ReadString(int offset)
        {
            int unused;
            return TraceEventRawReaders.ReadUnicodeString(this.userData, offset, out unused);
        }

        public string ReadString(int offset, out int endOffset)
        {
            return TraceEventRawReaders.ReadUnicodeString(this.userData, offset, out endOffset);
        }

        public uint ReadUInt(int offset)
        {
            return TraceEventRawReaders.ReadUInt32(this.userData, offset);
        }

        public ulong ReadULong(int offset)
        {
            return TraceEventRawReaders.ReadUInt64(this.userData, offset);
        }

        public ushort ReadUShort(int offset)
        {
            return TraceEventRawReaders.ReadUInt16(this.userData, offset);
        }

        public double ReadDouble(int offset)
        {
            return TraceEventRawReaders.ReadDouble(this.userData, offset);
        }

        unsafe private static void CopyBlob(IntPtr source, IntPtr destination, int byteCount)
        {
            int* sourcePtr = (int*)source;
            int* destationPtr = (int*)destination;
            int intCount = byteCount >> 2;
            while (intCount > 0)
            {
                *destationPtr++ = *sourcePtr++;
                --intCount;
            }
        }

        private unsafe EventData InternalClone()
        {
            if (record == null)
            {
                throw new InvalidOperationException("Attempted to clone event data with no underlying record");
            }

            var copy = (EventData)this.MemberwiseClone();
            // Copy the record, and the user date;

            if (record != null)
            {
                int userDataLength = (UserDataLength + 3) / 4 * 4;            // DWORD align
                int totalLength = sizeof(TraceEventNativeMethods.EVENT_RECORD) + userDataLength;

                IntPtr eventRecordBuffer = Marshal.AllocHGlobal(totalLength);

                IntPtr userDataBuffer = (IntPtr)(((byte*)eventRecordBuffer) + sizeof(TraceEventNativeMethods.EVENT_RECORD));

                CopyBlob((IntPtr)record, eventRecordBuffer, sizeof(TraceEventNativeMethods.EVENT_RECORD));
                CopyBlob(userData, userDataBuffer, userDataLength);

                copy.record = (TraceEventNativeMethods.EVENT_RECORD*)eventRecordBuffer;
                copy.userData = userDataBuffer;
                copy.cloneBuffer = eventRecordBuffer;

                // Read the callstack data before cloning event. We will have to read the memory for cloning regardless so
                // instead of doing a deep clone of data structures we might as well extract the frames here
                copy.callstackFramesExtendedData = this.GetCallstackExtendedData();
            }

            return copy;
        }
        public unsafe override string ToString()
        {
            var result = string.Empty;
            if (record != null)
            {
                result = string.Format("Pid = {0}, OpCode={1} Id = {2}",
                    record->EventHeader.ProcessId,
                    record->EventHeader.Opcode,
                    record->EventHeader.Id
                    );
            }
            return result;
        }
    }
    internal sealed class TraceEventRawReaders
    {
        unsafe internal static IntPtr Add(IntPtr pointer, int offset)
        {
            return (IntPtr)(((byte*)pointer) + offset);
        }
        unsafe internal static Guid ReadGuid(IntPtr pointer, int offset)
        {
            return *((Guid*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static double ReadDouble(IntPtr pointer, int offset)
        {
            return *((double*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static float ReadSingle(IntPtr pointer, int offset)
        {
            return *((float*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static long ReadInt64(IntPtr pointer, int offset)
        {
            return *((long*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static int ReadInt32(IntPtr pointer, int offset)
        {
            return *((int*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static uint ReadUInt32(IntPtr pointer, int offset)
        {
            return *((uint*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static ulong ReadUInt64(IntPtr pointer, int offset)
        {
            return *((ulong*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static short ReadInt16(IntPtr pointer, int offset)
        {
            return *((short*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static ushort ReadUInt16(IntPtr pointer, int offset)
        {
            return *((ushort*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static IntPtr ReadIntPtr(IntPtr pointer, int offset)
        {
            return *((IntPtr*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static byte ReadByte(IntPtr pointer, int offset)
        {
            return *((byte*)((byte*)pointer.ToPointer() + offset));
        }
        unsafe internal static byte[] ReadBytes(IntPtr pointer, int offset, ushort length)
        {
            byte[] data = new byte[length];
            Marshal.Copy(IntPtr.Add(pointer, offset), data, 0, length);
            return data;
        }
        unsafe internal static string ReadUnicodeString(IntPtr pointer, int offset, out int nextOffset)
        {

            // TODO in debug mode, insure string are inside buffer. 
            string str = new string((char*)((byte*)pointer.ToPointer() + offset));
            nextOffset = (offset + ((str.Length * sizeof(char)))) + sizeof(char);

            // TODO investigate this why does this happen?
#if DEBUG
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if ((c < ' ' || c > '~') && !char.IsWhiteSpace(c))
                {
                    str = str.Substring(0, i);
                    Debug.WriteLine("Warning: truncating " + (str.Length - i).ToString() + " non-ascii name characters: resulting string: " + str);
                    break;
                }
            }
#endif
            return str;
        }
        unsafe internal static string ReadAsciiString(IntPtr pointer, int offset)
        {
            // TODO in debug mode, insure string are inside buffer. 
            string ret = Marshal.PtrToStringAnsi((IntPtr)(pointer.ToInt64() + offset));
            return ret;
        }
    }

    public interface IEventUserData
    {
        ushort UserDataLength { get; }
        bool HasUserData { get; }

        long ReadLong(int offset);
        ulong ReadULong(int offset);
        IntPtr ReadIntPtr(int offset);
        uint ReadUInt(int offset);
        short ReadShort(int offset);
        ushort ReadUShort(int offset);
        int ReadInt(int offset);
        string ReadString(int offset);
        string ReadString(int offset, out int endOffset);
        Guid ReadGuid(int offset);
        byte ReadByte(int offset);
        byte[] ReadBytes(int offset, ushort length);
        double ReadDouble(int offset);
        int GetStringEndOffset(int offset);
    }

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
            var ProcToFilterTo = Process.GetProcessesByName("devenv").Skip(1).First();
            //            PidToFilterTo = 0;
            Trace.WriteLine($"Filtering to Pid {ProcToFilterTo.Id} {ProcToFilterTo.MainWindowTitle}");
            EVENT_FILTER_DESCRIPTOR* pFilterDesc = null;
            if (ProcToFilterTo != null)
            {
                traceParams.FilterDescCount = 1;
                pFilterDesc = (EVENT_FILTER_DESCRIPTOR*)Marshal.AllocCoTaskMem(sizeof(EVENT_FILTER_DESCRIPTOR));
                var parrPid = (int*)Marshal.AllocCoTaskMem(sizeof(int));
                *parrPid = ProcToFilterTo.Id;
                pFilterDesc->Ptr = (byte*)parrPid;

                pFilterDesc->Size = 8;
                pFilterDesc->Type = (int)EVENT_FILTER_TYPE_PID;
                traceParams.EnableFilterDesc = pFilterDesc;
            }

            int err = TraceEventNativeMethods.EnableTraceEx2(
                this.traceHandle,
                ref settings.Guid,
                TraceEventNativeMethods.EVENT_CONTROL_CODE_ENABLE_PROVIDER,
                (byte)settings.Level,
                settings.MatchAny,
                settings.MatchAll,
                Timeout: 0,
                EnableParameters: ref traceParams);
            if (pFilterDesc != null)
            {
                Marshal.FreeCoTaskMem((IntPtr)pFilterDesc->Ptr);
                Marshal.FreeCoTaskMem((IntPtr)pFilterDesc);
            }
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
                Trace.WriteLine("Stopping Trace Listener");
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
                    processWorkerThread.Join(TimeSpan.FromSeconds(20));
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
            Trace.WriteLine("Begin processing trace");
            // This is a blocking call. It will end when the trace is closed or 
            // TraceEventBufferCallback returns false. 
            int error = TraceEventNativeMethods.ProcessTrace(new ulong[] { this.logHandle }, 1, IntPtr.Zero, IntPtr.Zero);

            if (error != 0)
            {
                throw new Win32Exception(error);
            }

            //// check if the VS session is still active which would indicate the ETL session was terminated abnormally
            //var telemetryService = this.serviceProvider.GetService<TelemetryService>();
            //var processService = this.serviceProvider.GetService<ProcessService>();
            //if (processService != null
            //    && telemetryService != null
            //    && telemetryService.Targets.Any())
            //{
            //    var telemetrySession = telemetryService.Targets.First();
            //    if (processService.IsProcessRunning(telemetrySession.RootProcess.ProcessId))
            //    {
            //        telemetrySession.WasAbnormalTermination = true;
            //        var etlExited = new AppInsights.TelemetryEvent("VS/PerfWatson2/ETLSessionAbnormalShutdown");
            //        etlExited.Properties["VS.PerfWatson2.FailedProcessName"] = telemetrySession.RootProcess.ProcessName;
            //        AISessionHelper.PostEvent(etlExited);
            //    }
            //}
        }

        [AllowReversePInvokeCalls]
        private bool TraceEventBufferCallback(IntPtr rawLogFile)
        {
            return isTracing;
        }

        private void UpdateIsCollectionDisabled()
        {
            //if (this.serviceProvider != null)
            //{
            //    if (this.isTracing)
            //    {
            //        if (telemetryCollectionPolicy.ShouldStopCollection)
            //        {
            //            Trace.WriteLine("Stopping Collection");
            //            checkIfUpdateIsDisabled.Change(Timeout.Infinite, Timeout.Infinite);
            //            this.End();

            //            // downcast to see if we have concrete types needed to log reason for disabling collection
            //            var telemetryCollectionPolicyImpl = telemetryCollectionPolicy as TelemetryCollectionPolicy;
            //            var telemetryService = serviceProvider as TelemetryService;
            //            if (telemetryCollectionPolicyImpl != null && telemetryService != null
            //                && telemetryService.Targets != null && telemetryService.Targets.Any())
            //            {
            //                var defaultSession = telemetryService.Targets.First();
            //                var context = defaultSession.GetService<SessionContextService>();
            //                if (context != null && !context.Disposed)
            //                {
            //                    if (telemetryCollectionPolicyImpl.IsDebuggerAttached ?? false)
            //                    {
            //                        context.SetSystemValue("IsDebuggerAttached", "true");
            //                    }

            //                    if (telemetryCollectionPolicyImpl.HasReachedMaximumTelemetryDatabaseSize ?? false)
            //                    {
            //                        context.SetSystemValue("IsMaxFileSize", "true");
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}
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
