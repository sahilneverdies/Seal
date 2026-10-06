

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Seal {

    public static class NetVitals {
        static long lastRecv = -1, lastSent = -1;
        static DateTime lastTime = DateTime.MinValue;
        public static double UploadMbps { get; private set; }
        public static double DownloadMbps { get; private set; }

        public static void Poll() {
            try {
                long curRecv = 0, curSent = 0;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback) {
                        var stats = ni.GetIPStatistics();
                        curRecv += stats.BytesReceived;
                        curSent += stats.BytesSent;
                    }
                }
                DateTime now = DateTime.Now;
                if (lastTime != DateTime.MinValue && now > lastTime) {
                    double sec = (now - lastTime).TotalSeconds;
                    if (sec > 0.2 && lastRecv >= 0 && lastSent >= 0) {
                        DownloadMbps = Math.Max(0, ((curRecv - lastRecv) * 8.0) / (sec * 1000000.0));
                        UploadMbps = Math.Max(0, ((curSent - lastSent) * 8.0) / (sec * 1000000.0));
                    }
                }
                lastRecv = curRecv;
                lastSent = curSent;
                lastTime = now;
            } catch { }
        }
    }

    public static class FpsVitals {
        const string SESSION_NAME = "SealEtwFps";

        static readonly Guid DXGI_PROVIDER = new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
        static readonly Guid D3D9_PROVIDER = new Guid("783ACA0A-790E-4D7F-8451-AA850511C6B9");

        [StructLayout(LayoutKind.Sequential)]
        struct WNODE_HEADER {
            public uint BufferSize;
            public uint ProviderId;
            public ulong HistoricalContext;
            public ulong TimeStamp;
            public Guid Guid;
            public uint ClientContext;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct EVENT_TRACE_PROPERTIES {
            public WNODE_HEADER Wnode;
            public uint BufferSize;
            public uint MinimumBuffers;
            public uint MaximumBuffers;
            public uint MaximumFileSize;
            public uint LogFileMode;
            public uint FlushTimer;
            public uint EnableFlags;
            public int AgeLimit;
            public uint NumberOfBuffers;
            public uint FreeBuffers;
            public uint EventsLost;
            public uint BuffersWritten;
            public uint LogBuffersLost;
            public uint RealTimeBuffersLost;
            public IntPtr LoggerThreadId;
            public uint LogFileNameOffset;
            public uint LoggerNameOffset;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct EVENT_TRACE_HEADER {
            public ushort Size;
            public ushort FieldTypeFlags;
            public uint Version;
            public uint ThreadId;
            public uint ProcessId;
            public long TimeStamp;
            public Guid Guid;
            public ulong ProcessorTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct EVENT_TRACE {
            public EVENT_TRACE_HEADER Header;
            public uint InstanceId;
            public uint ParentInstanceId;
            public Guid ParentGuid;
            public IntPtr MofData;
            public uint MofLength;
            public uint BufferContext;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct TIME_ZONE_INFORMATION {
            public int Bias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string StandardName;
            public short StandardYear, StandardMonth, StandardDayOfWeek, StandardDay, StandardHour, StandardMinute, StandardSecond, StandardMilliseconds;
            public int StandardBias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DaylightName;
            public short DaylightYear, DaylightMonth, DaylightDayOfWeek, DaylightDay, DaylightHour, DaylightMinute, DaylightSecond, DaylightMilliseconds;
            public int DaylightBias;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct TRACE_LOGFILE_HEADER {
            public uint BufferSize;
            public uint Version;
            public uint ProviderVersion;
            public uint NumberOfProcessors;
            public long EndTime;
            public uint TimerResolution;
            public uint MaximumFileSize;
            public uint LogFileMode;
            public uint BuffersWritten;
            public uint StartBuffers;
            public uint PointerSize;
            public uint EventsLost;
            public uint CpuSpeedInMHz;
            public IntPtr LoggerName;
            public IntPtr LogFileName;
            public TIME_ZONE_INFORMATION TimeZone;
            public long BootTime;
            public long PerfFreq;
            public long StartTime;
            public uint ReservedFlags;
            public uint BuffersLost;
        }

        delegate void EventRecordCallback(IntPtr pRecord);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct EVENT_TRACE_LOGFILEW {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LogFileName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LoggerName;
            public long CurrentTime;
            public uint BuffersRead;
            public uint ProcessTraceMode;
            public EVENT_TRACE CurrentEvent;
            public TRACE_LOGFILE_HEADER LogfileHeader;
            public IntPtr BufferCallback;
            public uint BufferSize;
            public uint Filled;
            public uint EventsLost;
            public EventRecordCallback EventRecordCallback;
            public uint IsKernelTrace;
            public IntPtr Context;
        }

        const uint EVENT_TRACE_REAL_TIME_MODE = 0x00000100;
        const uint WNODE_FLAG_TRACED_GUID = 0x00020000;
        const uint EVENT_CONTROL_CODE_ENABLE_PROVIDER = 1;
        const uint EVENT_CONTROL_CODE_STOP_TRACE = 1;
        const uint PROCESS_TRACE_MODE_REAL_TIME = 0x00000100;
        const uint PROCESS_TRACE_MODE_EVENT_RECORD = 0x10000000;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int StartTraceW(out ulong sessionHandle, string sessionName, IntPtr properties);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int ControlTraceW(ulong sessionHandle, string sessionName, IntPtr properties, uint controlCode);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int EnableTraceEx2(ulong sessionHandle, ref Guid providerId, uint controlCode, byte level, ulong matchAnyKeyword, ulong matchAllKeyword, uint timeout, IntPtr enableParameters);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern ulong OpenTraceW(ref EVENT_TRACE_LOGFILEW logfile);

        [DllImport("advapi32.dll")]
        static extern int ProcessTrace(ulong[] handleArray, uint handleCount, IntPtr startTime, IntPtr endTime);

        [DllImport("advapi32.dll")]
        static extern int CloseTrace(ulong traceHandle);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);


        static bool started = false;
        static ulong sessionHandle = 0;
        static ulong traceHandle = 0;
        static IntPtr pProps = IntPtr.Zero;
        static Thread traceThread = null;
        static EventRecordCallback callbackDelegate;
        static readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;


        static readonly ConcurrentDictionary<uint, long> frameCounts = new ConcurrentDictionary<uint, long>();
        static readonly Dictionary<uint, long> prevFrameCounts = new Dictionary<uint, long>();
        static DateTime lastPollTime = DateTime.UtcNow;
        static int noGameTicks = 0;

        static int currentFps = 0;
        static string currentFpsText = "--";
        public static int CurrentFps { get { return currentFps; } private set { currentFps = value; } }
        public static string CurrentFpsText { get { return currentFpsText; } private set { currentFpsText = value; } }

        static void OnEvent(IntPtr pRecord) {
            try {
                if (pRecord == IntPtr.Zero) return;
                ushort eventId = (ushort)Marshal.ReadInt16(pRecord, 40);

                if (eventId == 42 || eventId == 39 || eventId == 1) {
                    uint pid = (uint)Marshal.ReadInt32(pRecord, 12);
                    if (pid != ownPid && pid > 4) {
                        frameCounts.AddOrUpdate(pid, 1, (k, v) => v + 1);
                    }
                }
            } catch { }
        }

        public static void Start() {
            if (started) return;
            started = true;
            try {
                AppDomain.CurrentDomain.ProcessExit += (s, e) => Stop();

                int structSize = Marshal.SizeOf(typeof(EVENT_TRACE_PROPERTIES));
                int totalSize = structSize + 1024;
                pProps = Marshal.AllocHGlobal(totalSize);
                for (int i = 0; i < totalSize; i++) Marshal.WriteByte(pProps, i, 0);

                var props = new EVENT_TRACE_PROPERTIES();
                props.Wnode.BufferSize = (uint)totalSize;
                props.Wnode.Flags = WNODE_FLAG_TRACED_GUID;
                props.Wnode.ClientContext = 1;
                props.LogFileMode = EVENT_TRACE_REAL_TIME_MODE;
                props.LoggerNameOffset = (uint)structSize;
                props.LogFileNameOffset = 0;
                Marshal.StructureToPtr(props, pProps, false);


                ControlTraceW(0, SESSION_NAME, pProps, EVENT_CONTROL_CODE_STOP_TRACE);

                int status = StartTraceW(out sessionHandle, SESSION_NAME, pProps);
                if (status != 0) {
                    Marshal.FreeHGlobal(pProps);
                    pProps = IntPtr.Zero;
                    return;
                }


                Guid dxgi = DXGI_PROVIDER;
                EnableTraceEx2(sessionHandle, ref dxgi, EVENT_CONTROL_CODE_ENABLE_PROVIDER, 4, 0xFFFFFFFFFFFFFFFF, 0, 0, IntPtr.Zero);
                Guid d3d9 = D3D9_PROVIDER;
                EnableTraceEx2(sessionHandle, ref d3d9, EVENT_CONTROL_CODE_ENABLE_PROVIDER, 4, 0xFFFFFFFFFFFFFFFF, 0, 0, IntPtr.Zero);

                callbackDelegate = OnEvent;
                var logfile = new EVENT_TRACE_LOGFILEW {
                    LoggerName = SESSION_NAME,
                    ProcessTraceMode = PROCESS_TRACE_MODE_REAL_TIME | PROCESS_TRACE_MODE_EVENT_RECORD,
                    EventRecordCallback = callbackDelegate
                };

                traceHandle = OpenTraceW(ref logfile);
                if (traceHandle == 0 || traceHandle == unchecked((ulong)-1)) {
                    Stop();
                    return;
                }

                traceThread = new Thread(() => {
                    try {
                        ulong[] handles = new ulong[] { traceHandle };
                        ProcessTrace(handles, 1, IntPtr.Zero, IntPtr.Zero);
                    } catch { }
                }) {
                    IsBackground = true,
                    Name = "SealEtwFpsThread"
                };
                traceThread.Start();
            } catch { }
        }

        public static void Stop() {
            try {
                if (traceHandle != 0 && traceHandle != unchecked((ulong)-1)) {
                    CloseTrace(traceHandle);
                    traceHandle = 0;
                }
                if (pProps != IntPtr.Zero) {
                    ControlTraceW(sessionHandle, SESSION_NAME, pProps, EVENT_CONTROL_CODE_STOP_TRACE);
                    Marshal.FreeHGlobal(pProps);
                    pProps = IntPtr.Zero;
                }
                sessionHandle = 0;
            } catch { }
        }

        public static void Poll(IntPtr hwnd) {
            try {
                if (!started) Start();

                DateTime now = DateTime.UtcNow;
                double dt = (now - lastPollTime).TotalSeconds;
                if (dt < 0.25) return;
                lastPollTime = now;

                IntPtr fg = GetForegroundWindow();
                uint fgPid = 0;
                if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out fgPid);

                int fgFps = 0;
                int maxOtherFps = 0;

                var snapshot = frameCounts.ToArray();
                foreach (var kvp in snapshot) {
                    uint pid = kvp.Key;
                    long curFrames = kvp.Value;
                    long prevFrames;
                    prevFrameCounts.TryGetValue(pid, out prevFrames);
                    prevFrameCounts[pid] = curFrames;

                    if (curFrames >= prevFrames) {
                        int fps = (int)Math.Round((curFrames - prevFrames) / dt);
                        if (fps > 0) {
                            if (pid == fgPid) {
                                fgFps = fps;
                            } else if (fps > maxOtherFps && pid != ownPid) {
                                maxOtherFps = fps;
                            }
                        }
                    }
                }

                int resolvedFps = fgFps > 0 ? fgFps : maxOtherFps;

                if (resolvedFps > 0) {
                    CurrentFps = resolvedFps;
                    CurrentFpsText = resolvedFps.ToString();
                    noGameTicks = 0;
                } else {
                    if (++noGameTicks >= 2) {
                        CurrentFps = 0;
                        CurrentFpsText = "--";
                    }
                }
            } catch { }
        }
    }

    public sealed class OverlayWindow : Window {
        readonly Engine E;
        readonly DispatcherTimer timer;
        IntPtr hwnd;


        readonly Border rootBox;
        readonly Grid mainGrid;
        readonly Border popupBox;
        bool popupOpen;


        readonly Border btnPin, btnMenu;
        readonly Path pathPin;


        readonly Border perfGroup;
        readonly Border btnPerfEco, btnPerfQuiet, btnPerfBal, btnPerfMax;
        readonly TextBlock txtPerfEco, txtPerfQuiet, txtPerfBal, txtPerfMax;


        readonly FrameworkElement blockCpu, blockGpu, blockRam, blockFps, blockNet;
        readonly TextBlock txtCpuVal1, txtCpuVal2;
        readonly TextBlock txtGpuVal1, txtGpuVal2;
        readonly TextBlock txtRamVal;
        readonly TextBlock txtFpsVal;
        readonly TextBlock txtNetUp, txtNetDown;


        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int idx, int val);

        public const string PATH_PIN = "M16 12V4H17V2H7V4H8V12L6 14V16H11V22L12 23L13 22V16H18V14L16 12Z";
        public const string PATH_MENU = "M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z";
        public const string PATH_GAUGE = "M12 4 C7.03 4 3 8.03 3 13 C3 15.5 4.02 17.76 5.67 19.38 L7.08 17.97 C5.79 16.69 5 14.94 5 13 C5 9.13 8.13 6 12 6 C15.87 6 19 9.13 19 13 C19 14.94 18.21 16.69 16.92 17.97 L18.33 19.38 C19.98 17.76 21 15.5 21 13 C21 8.03 16.97 4 12 4 Z M12 8 C11.45 8 11 8.45 11 9 L11 12.59 L8.71 14.88 L10.12 16.29 L12.71 13.71 C12.89 13.53 13 13.28 13 13 C13 12.45 12.55 12 12 12 L12 9 C12 8.45 11.55 8 12 8 Z";

        public Action<bool> OnPerfVisibilityChanged;
        public Action<bool> OnPinnedChanged;

        public static void AttachClick(FrameworkElement el, Action onClick) {
            if (el == null) return;
            el.PreviewMouseLeftButtonDown += (s, e) => {
                e.Handled = true;
            };
            el.MouseLeftButtonDown += (s, e) => {
                e.Handled = true;
            };
            el.PreviewMouseLeftButtonUp += (s, e) => {
                e.Handled = true;
                try { onClick(); } catch { }
            };
            el.MouseLeftButtonUp += (s, e) => {
                e.Handled = true;
            };
        }

        public void UpdatePinState() {
            Topmost = E.S.OverlayPinned;
            if (pathPin != null) pathPin.Fill = E.S.OverlayPinned ? Ui.Brush(Ui.Accent.Color) : Ui.Brush("#8A8581");
        }

        public OverlayWindow(Engine engine) {
            E = engine;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = E.S.OverlayPinned;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            rootBox = new Border {
                Background = Ui.Brush("#E612100E"),
                BorderBrush = Ui.Brush("#343029"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                SnapsToDevicePixels = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.5, Color = Colors.Black }
            };

            mainGrid = new Grid();
            rootBox.Child = mainGrid;


            pathPin = new Path { Data = Geometry.Parse(PATH_PIN), Width = 13, Height = 13, Stretch = Stretch.Uniform, Fill = E.S.OverlayPinned ? Ui.Brush(Ui.Accent.Color) : Ui.Brush("#8A8581") };
            btnPin = new Border {
                Background = Ui.Brush("#201C19"),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 4, 6, 4),
                Cursor = Cursors.Hand,
                ToolTip = "Pin Overlay on Top of games & desktop",
                Child = pathPin,
                Margin = new Thickness(0, 0, 6, 0)
            };
            AttachClick(btnPin, () => {
                E.S.OverlayPinned = !E.S.OverlayPinned;
                if (E.S.OverlayPinned) {
                    E.S.OverlayPerfPinned = false;
                }
                UpdatePinState();
                E.S.Save();
                if (OnPinnedChanged != null) OnPinnedChanged(E.S.OverlayPinned);
            });


            btnMenu = new Border {
                Background = Ui.Brush("#201C19"),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 4, 6, 4),
                Cursor = Cursors.Hand,
                ToolTip = "System Vitals & Performance Options",
                Child = new Path { Data = Geometry.Parse(PATH_MENU), Width = 13, Height = 13, Stretch = Stretch.Uniform, Fill = Ui.Brush("#C0BAB4") },
                Margin = new Thickness(0, 0, 8, 0)
            };
            AttachClick(btnMenu, () => {
                TogglePopup();
            });


            perfGroup = new Border {
                Background = Ui.Brush("#161311"),
                BorderBrush = Ui.Brush("#272320"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var perfStack = new StackPanel { Orientation = Orientation.Horizontal };
            perfGroup.Child = perfStack;

            btnPerfEco = MakePerfPill("Eco", out txtPerfEco, () => E.SetOverlayPerf(0));
            btnPerfQuiet = MakePerfPill("Quiet", out txtPerfQuiet, () => E.SetOverlayPerf(1));
            btnPerfBal = MakePerfPill("Balanced", out txtPerfBal, () => E.SetOverlayPerf(2));
            btnPerfMax = MakePerfPill("Perf", out txtPerfMax, () => E.SetOverlayPerf(3));

            perfStack.Children.Add(btnPerfEco);
            perfStack.Children.Add(btnPerfQuiet);
            perfStack.Children.Add(btnPerfBal);
            perfStack.Children.Add(btnPerfMax);


            blockCpu = MakeMetricBlock("CPU", out txtCpuVal1, out txtCpuVal2);
            blockGpu = MakeMetricBlock("GPU", out txtGpuVal1, out txtGpuVal2);
            blockRam = MakeMetricSingleBlock("RAM", out txtRamVal);
            blockFps = MakeMetricSingleBlock("FPS", out txtFpsVal);
            blockNet = MakeNetBlock(out txtNetUp, out txtNetDown);


            popupBox = BuildPopupBox();
            popupBox.Visibility = Visibility.Collapsed;

            Panel.SetZIndex(rootBox, 1);
            Panel.SetZIndex(popupBox, 100);

            var outerGrid = new Grid();
            outerGrid.Children.Add(rootBox);
            outerGrid.Children.Add(popupBox);
            Content = outerGrid;


            rootBox.MouseLeftButtonDown += (s, e) => {
                if (e.LeftButton == MouseButtonState.Pressed && !e.Handled) {
                    try { DragMove(); } catch { }
                    E.S.OverlayX = Left;
                    E.S.OverlayY = Top;
                    E.S.Save();
                }
            };

            SourceInitialized += delegate {
                hwnd = new WindowInteropHelper(this).Handle;

                SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x00000080);
                RestorePosition();
            };


            ApplyLayout();
            UpdateVitals();


            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            timer.Tick += (s, e) => {
                NetVitals.Poll();
                FpsVitals.Poll(hwnd);
                UpdateVitals();
            };
            timer.Start();

            Closed += (s, e) => {
                timer.Stop();
                FpsVitals.Stop();
            };
        }

        Border MakePerfPill(string label, out TextBlock txt, Action onClick) {
            txt = new TextBlock {
                Text = label,
                FontFamily = Ui.UiFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.Brush("#8A8581"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var pill = new Border {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(1, 0, 1, 0),
                Cursor = Cursors.Hand,
                Child = txt
            };
            AttachClick(pill, onClick);
            return pill;
        }

        FrameworkElement MakeMetricBlock(string label, out TextBlock val1, out TextBlock val2) {
            var b = new Border {
                Background = Ui.Brush("#1A1614"),
                BorderBrush = Ui.Brush("#272320"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var lbl = new TextBlock { Text = label, FontFamily = Ui.UiFont, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Ui.Brush("#8A8581"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            val1 = new TextBlock { Text = "--", FontFamily = Ui.MonoFont, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#EDEAE8"), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            val2 = new TextBlock { Text = "--", FontFamily = Ui.MonoFont, FontSize = 12, Foreground = Ui.Brush("#A29D99"), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(lbl);
            sp.Children.Add(val1);
            sp.Children.Add(val2);
            b.Child = sp;
            return b;
        }

        FrameworkElement MakeMetricSingleBlock(string label, out TextBlock val) {
            var b = new Border {
                Background = Ui.Brush("#1A1614"),
                BorderBrush = Ui.Brush("#272320"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var lbl = new TextBlock { Text = label, FontFamily = Ui.UiFont, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Ui.Brush("#8A8581"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            val = new TextBlock { Text = "--", FontFamily = Ui.MonoFont, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#EDEAE8"), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(lbl);
            sp.Children.Add(val);
            b.Child = sp;
            return b;
        }

        FrameworkElement MakeNetBlock(out TextBlock up, out TextBlock down) {
            var b = new Border {
                Background = Ui.Brush("#1A1614"),
                BorderBrush = Ui.Brush("#272320"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var lblUp = new TextBlock { Text = "↑", FontSize = 11, Foreground = Ui.Brush("#3F8CFF"), Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
            up = new TextBlock { Text = "0.0 Mbps", FontFamily = Ui.MonoFont, FontSize = 11.5, Foreground = Ui.Brush("#EDEAE8"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var lblDown = new TextBlock { Text = "↓", FontSize = 11, Foreground = Ui.Brush("#4AC06C"), Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
            down = new TextBlock { Text = "0.0 Mbps", FontFamily = Ui.MonoFont, FontSize = 11.5, Foreground = Ui.Brush("#EDEAE8"), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(lblUp);
            sp.Children.Add(up);
            sp.Children.Add(lblDown);
            sp.Children.Add(down);
            b.Child = sp;
            return b;
        }

        Action refreshPopup;

        Border BuildPopupBox() {
            var box = new Border {
                Background = Ui.Brush("#F4161311"),
                BorderBrush = Ui.Brush("#343029"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Width = 230,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 38, 0, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.7, Color = Colors.Black }
            };
            box.MouseLeftButtonDown += (s, e) => e.Handled = true;

            var stack = new StackPanel();


            var head = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            var headLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconGauge = new Path { Data = Geometry.Parse(PATH_GAUGE), Width = 14, Height = 14, Stretch = Stretch.Uniform, Fill = Ui.Brush(Ui.Accent.Color), Margin = new Thickness(0, 0, 6, 0) };
            var title = new TextBlock { Text = "System Vitals", FontFamily = Ui.UiFont, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#EDEAE8"), VerticalAlignment = VerticalAlignment.Center };
            headLeft.Children.Add(iconGauge);
            headLeft.Children.Add(title);

            var closeBtn = new TextBlock { Text = "✕", FontSize = 12, Foreground = Ui.Brush("#8A8581"), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(4) };
            AttachClick(closeBtn, () => TogglePopup());

            head.Children.Add(headLeft);
            head.Children.Add(closeBtn);
            stack.Children.Add(head);


            var orientLabel = new TextBlock { Text = "ORIENTATION", FontFamily = Ui.UiFont, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#7A7570"), Margin = new Thickness(0, 0, 0, 6) };
            stack.Children.Add(orientLabel);

            var orientRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            orientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            orientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            orientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var txtBar = new TextBlock { Text = "Horizontal", FontFamily = Ui.UiFont, FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var btnBar = new Border {
                CornerRadius = new CornerRadius(5),
                Height = 28,
                Cursor = Cursors.Hand,
                Child = txtBar
            };

            var txtGrid = new TextBlock { Text = "2-Col Grid", FontFamily = Ui.UiFont, FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var btnGrid = new Border {
                CornerRadius = new CornerRadius(5),
                Height = 28,
                Cursor = Cursors.Hand,
                Child = txtGrid
            };

            Action refreshOrient = delegate {
                if (E.S.OverlayHorizontal) {
                    btnBar.Background = Ui.Brush(Ui.Accent.Color);
                    txtBar.Foreground = Brushes.White;
                    btnGrid.Background = Ui.Brush("#201C19");
                    txtGrid.Foreground = Ui.Brush("#8A8581");
                } else {
                    btnBar.Background = Ui.Brush("#201C19");
                    txtBar.Foreground = Ui.Brush("#8A8581");
                    btnGrid.Background = Ui.Brush(Ui.Accent.Color);
                    txtGrid.Foreground = Brushes.White;
                }
            };

            AttachClick(btnBar, delegate {
                E.S.OverlayHorizontal = true;
                E.S.Save();
                refreshOrient();
                ApplyLayout();
            });

            AttachClick(btnGrid, delegate {
                E.S.OverlayHorizontal = false;
                E.S.Save();
                refreshOrient();
                ApplyLayout();
            });

            Grid.SetColumn(btnBar, 0); orientRow.Children.Add(btnBar);
            Grid.SetColumn(btnGrid, 2); orientRow.Children.Add(btnGrid);
            stack.Children.Add(orientRow);


            stack.Children.Add(new Rectangle { Height = 1, Fill = Ui.Brush("#272320"), Margin = new Thickness(0, 0, 0, 8) });

            var displayLabel = new TextBlock { Text = "DISPLAY METRICS", FontFamily = Ui.UiFont, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#7A7570"), Margin = new Thickness(0, 0, 0, 4) };
            stack.Children.Add(displayLabel);


            Action rCpu, rGpu, rRam, rFps, rUp, rDown, rPerf;
            stack.Children.Add(MakeCheckRow("CPU Temp & Load", () => E.S.OverlayShowCpu, (b) => { E.S.OverlayShowCpu = b; E.S.Save(); ApplyLayout(); }, out rCpu));
            stack.Children.Add(MakeCheckRow("GPU Temp & Load", () => E.S.OverlayShowGpu, (b) => { E.S.OverlayShowGpu = b; E.S.Save(); ApplyLayout(); }, out rGpu));
            stack.Children.Add(MakeCheckRow("RAM Memory %", () => E.S.OverlayShowRam, (b) => { E.S.OverlayShowRam = b; E.S.Save(); ApplyLayout(); }, out rRam));
            stack.Children.Add(MakeCheckRow("FPS In-Game", () => E.S.OverlayShowFps, (b) => { E.S.OverlayShowFps = b; E.S.Save(); ApplyLayout(); }, out rFps));
            stack.Children.Add(MakeCheckRow("Upload Speed", () => E.S.OverlayShowUpload, (b) => { E.S.OverlayShowUpload = b; E.S.Save(); ApplyLayout(); }, out rUp));
            stack.Children.Add(MakeCheckRow("Download Speed", () => E.S.OverlayShowDownload, (b) => { E.S.OverlayShowDownload = b; E.S.Save(); ApplyLayout(); }, out rDown));
            stack.Children.Add(MakeCheckRow("Performance Control", () => E.S.OverlayShowPerf, (b) => {
                E.S.OverlayShowPerf = b;
                E.S.Save();
                if (OnPerfVisibilityChanged != null) OnPerfVisibilityChanged(b);
            }, out rPerf));

            refreshPopup = delegate {
                refreshOrient();
                if (rCpu != null) rCpu();
                if (rGpu != null) rGpu();
                if (rRam != null) rRam();
                if (rFps != null) rFps();
                if (rUp != null) rUp();
                if (rDown != null) rDown();
                if (rPerf != null) rPerf();
            };

            box.Child = stack;
            return box;
        }

        Border MakeCheckRow(string text, Func<bool> getVal, Action<bool> setVal, out Action refresh) {
            var row = new Border {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var box = new Border {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var checkTick = new Path {
                Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"),
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            box.Child = checkTick;

            var lbl = new TextBlock {
                Text = text,
                FontFamily = Ui.UiFont,
                FontSize = 12,
                Foreground = Ui.Brush("#EDEAE8"),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(box);
            sp.Children.Add(lbl);
            row.Child = sp;

            refresh = delegate {
                bool val = getVal();
                if (val) {
                    box.Background = Ui.Brush(Ui.Accent.Color);
                    box.BorderBrush = Ui.Brush(Ui.Accent.Color);
                    checkTick.Visibility = Visibility.Visible;
                } else {
                    box.Background = Ui.Brush("#1C1916");
                    box.BorderBrush = Ui.Brush("#443F3A");
                    checkTick.Visibility = Visibility.Collapsed;
                }
            };
            refresh();

            row.MouseEnter += (s, e) => row.Background = Ui.Brush("#231F1C");
            row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;

            AttachClick(row, delegate {
                bool next = !getVal();
                setVal(next);
            });

            return row;
        }

        void TogglePopup() {
            popupOpen = !popupOpen;
            if (popupOpen) {
                if (refreshPopup != null) refreshPopup();
                popupBox.Margin = E.S.OverlayHorizontal ? new Thickness(2, 38, 0, 0) : new Thickness(2, 28, 0, 0);
            }
            popupBox.Visibility = popupOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        static void Detach(UIElement el) {
            if (el == null) return;
            var fe = el as FrameworkElement;
            if (fe != null) {
                var p = fe.Parent as Panel;
                if (p != null) { p.Children.Remove(el); return; }
                var cc = fe.Parent as ContentControl;
                if (cc != null) { cc.Content = null; return; }
                var dec = fe.Parent as Decorator;
                if (dec != null) { dec.Child = null; return; }
            }
            var p1 = VisualTreeHelper.GetParent(el) as Panel;
            if (p1 != null) { try { p1.Children.Remove(el); } catch { } return; }
            var p2 = LogicalTreeHelper.GetParent(el) as Panel;
            if (p2 != null) { try { p2.Children.Remove(el); } catch { } return; }
        }

        public void ApplyLayout() {
            Detach(btnPin);
            Detach(btnMenu);
            Detach(perfGroup);
            Detach(blockCpu);
            Detach(blockGpu);
            Detach(blockRam);
            Detach(blockFps);
            Detach(blockNet);

            mainGrid.Children.Clear();
            mainGrid.RowDefinitions.Clear();
            mainGrid.ColumnDefinitions.Clear();

            rootBox.Opacity = Math.Max(0.2, Math.Min(1.0, E.S.OverlayOpacity / 100.0));

            if (refreshPopup != null) refreshPopup();

            blockCpu.Visibility = E.S.OverlayShowCpu ? Visibility.Visible : Visibility.Collapsed;
            blockGpu.Visibility = E.S.OverlayShowGpu ? Visibility.Visible : Visibility.Collapsed;
            blockRam.Visibility = E.S.OverlayShowRam ? Visibility.Visible : Visibility.Collapsed;
            blockFps.Visibility = E.S.OverlayShowFps ? Visibility.Visible : Visibility.Collapsed;
            blockNet.Visibility = (E.S.OverlayShowUpload || E.S.OverlayShowDownload) ? Visibility.Visible : Visibility.Collapsed;
            perfGroup.Visibility = E.S.OverlayShowPerf ? Visibility.Visible : Visibility.Collapsed;

            if (E.S.OverlayHorizontal) {

                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(btnPin);
                sp.Children.Add(btnMenu);
                if (E.S.OverlayShowCpu) sp.Children.Add(blockCpu);
                if (E.S.OverlayShowGpu) sp.Children.Add(blockGpu);
                if (E.S.OverlayShowRam) sp.Children.Add(blockRam);
                if (E.S.OverlayShowFps) sp.Children.Add(blockFps);
                if (E.S.OverlayShowUpload || E.S.OverlayShowDownload) sp.Children.Add(blockNet);
                mainGrid.Children.Add(sp);
            } else {

                var verticalStack = new StackPanel();


                var headerBar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                var leftGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                leftGroup.Children.Add(btnPin);

                var rightGroup = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                rightGroup.Children.Add(btnMenu);

                headerBar.Children.Add(leftGroup);
                headerBar.Children.Add(rightGroup);
                verticalStack.Children.Add(headerBar);


                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int r = 0;
                if (E.S.OverlayShowCpu || E.S.OverlayShowGpu) {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    if (E.S.OverlayShowCpu) { Grid.SetRow(blockCpu, r); Grid.SetColumn(blockCpu, 0); grid.Children.Add(blockCpu); }
                    if (E.S.OverlayShowGpu) { Grid.SetRow(blockGpu, r); Grid.SetColumn(blockGpu, 1); grid.Children.Add(blockGpu); }
                    r++;
                }
                if (E.S.OverlayShowRam || E.S.OverlayShowFps) {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    if (E.S.OverlayShowRam) { Grid.SetRow(blockRam, r); Grid.SetColumn(blockRam, 0); grid.Children.Add(blockRam); }
                    if (E.S.OverlayShowFps) { Grid.SetRow(blockFps, r); Grid.SetColumn(blockFps, 1); grid.Children.Add(blockFps); }
                    r++;
                }
                if (E.S.OverlayShowUpload || E.S.OverlayShowDownload) {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    Grid.SetRow(blockNet, r); Grid.SetColumnSpan(blockNet, 2); grid.Children.Add(blockNet);
                    r++;
                }

                verticalStack.Children.Add(grid);
                mainGrid.Children.Add(verticalStack);
            }
        }

        static SolidColorBrush TempBrush(double temp) {
            if (double.IsNaN(temp) || temp <= 0) return Ui.Brush("#EDEAE8");
            if (temp >= 90) return Ui.Brush(Ui.Danger);
            if (temp >= 80) return Ui.Brush(Ui.Warn);
            if (temp >= 65) return Ui.Brush("#E8A838");
            return Ui.Brush("#4AC06C");
        }

        public void UpdateVitals() {

            double cpuTemp = E.CpuTemp;
            double cpuLoad = E.CpuLoad;
            txtCpuVal1.Text = double.IsNaN(cpuTemp) ? "--" : cpuTemp.ToString("0") + "°";
            txtCpuVal1.Foreground = TempBrush(cpuTemp);
            txtCpuVal2.Text = double.IsNaN(cpuLoad) ? "--" : cpuLoad.ToString("0") + "%";


            double gpuTemp = E.GpuTemp;
            double gpuLoad = E.GpuLoad;
            txtGpuVal1.Text = double.IsNaN(gpuTemp) ? "--" : gpuTemp.ToString("0") + "°";
            txtGpuVal1.Foreground = TempBrush(gpuTemp);
            txtGpuVal2.Text = double.IsNaN(gpuLoad) ? "--" : gpuLoad.ToString("0") + "%";


            var mem = MemoryCleaner.GetStats();
            txtRamVal.Text = mem.UsedPercent + "%";
            txtRamVal.Foreground = mem.UsedPercent > 85 ? Ui.Brush(Ui.Danger) : mem.UsedPercent > 70 ? Ui.Brush(Ui.Warn) : Ui.Brush("#EDEAE8");


            txtFpsVal.Text = FpsVitals.CurrentFpsText;
            if (FpsVitals.CurrentFps > 0) {
                txtFpsVal.Foreground = FpsVitals.CurrentFps >= 60 ? Ui.Brush("#4AC06C") : (FpsVitals.CurrentFps >= 30 ? Ui.Brush(Ui.Warn) : Ui.Brush(Ui.Danger));
            } else {
                txtFpsVal.Foreground = Ui.Brush("#8E8B85");
            }


            txtNetUp.Text = NetVitals.UploadMbps.ToString("0.0") + " Mbps";
            txtNetDown.Text = NetVitals.DownloadMbps.ToString("0.0") + " Mbps";


        }

        public void RestorePosition() {
            var wa = SystemParameters.WorkArea;
            if (E.S.OverlayX >= 0 && E.S.OverlayY >= 0 && E.S.OverlayX < wa.Right - 50 && E.S.OverlayY < wa.Bottom - 30) {
                Left = E.S.OverlayX;
                Top = E.S.OverlayY;
            } else {

                Left = wa.Left + (wa.Width - 420) / 2;
                Top = wa.Top + 24;
            }
        }

        public void ResetPosition() {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - 420) / 2;
            Top = wa.Top + 24;
            E.S.OverlayX = Left;
            E.S.OverlayY = Top;
            E.S.Save();
        }
    }

    public class OverlayPerfWindow : Window {
        readonly Engine E;
        readonly Border rootBox;
        readonly Border btnEco, btnQuiet, btnBal, btnPerf;
        readonly TextBlock txtEco, txtQuiet, txtBal, txtPerf;
        readonly Border btnFanAuto, btnFanMax;
        readonly TextBlock txtFanAuto, txtFanMax, txtFanStatus;
        readonly Border btnPin;
        readonly Path pinPath;
        IntPtr hwnd;

        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public OverlayPerfWindow(Engine engine) {
            E = engine;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            Topmost = E.S.OverlayPerfPinned;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;

            rootBox = new Border {
                Background = Ui.Brush("#F4151210"),
                BorderBrush = Ui.Brush("#2E2925"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Width = 228,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.65, Color = Colors.Black }
            };

            var mainStack = new StackPanel();


            var head = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            var headLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var titleIcon = new Path { Data = Geometry.Parse(OverlayWindow.PATH_GAUGE), Width = 13, Height = 13, Stretch = Stretch.Uniform, Fill = Ui.Brush(Ui.Accent.Color), Margin = new Thickness(0, 0, 6, 0) };
            var headTitle = new TextBlock { Text = "Performance Control", FontFamily = Ui.UiFont, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#EDEAE8"), VerticalAlignment = VerticalAlignment.Center };
            headLeft.Children.Add(titleIcon);
            headLeft.Children.Add(headTitle);

            pinPath = new Path {
                Data = Geometry.Parse(OverlayWindow.PATH_PIN),
                Width = 11,
                Height = 11,
                Stretch = Stretch.Uniform,
                Fill = E.S.OverlayPerfPinned ? Ui.Brush(Ui.Accent.Color) : Ui.Brush("#8A8581")
            };
            btnPin = new Border {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2),
                Cursor = Cursors.Hand,
                ToolTip = "Keep on top",
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = pinPath
            };
            OverlayWindow.AttachClick(btnPin, delegate {
                E.S.OverlayPerfPinned = !E.S.OverlayPerfPinned;
                if (E.S.OverlayPerfPinned) {
                    E.S.OverlayPinned = false;
                }
                UpdatePinState();
                E.S.Save();
                if (OnPinnedChanged != null) OnPinnedChanged(E.S.OverlayPerfPinned);
            });

            head.Children.Add(headLeft);
            head.Children.Add(btnPin);
            mainStack.Children.Add(head);


            var gridModes = new Grid { Margin = new Thickness(0, 2, 0, 10) };
            gridModes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridModes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            gridModes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridModes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gridModes.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            gridModes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            btnEco = MakeModeCard("Eco", out txtEco, delegate { SetPerfMode(0); });
            btnQuiet = MakeModeCard("Quiet", out txtQuiet, delegate { SetPerfMode(1); });
            btnBal = MakeModeCard("Balanced", out txtBal, delegate { SetPerfMode(2); });
            btnPerf = MakeModeCard("Performance", out txtPerf, delegate { SetPerfMode(3); });

            Grid.SetRow(btnEco, 0); Grid.SetColumn(btnEco, 0); gridModes.Children.Add(btnEco);
            Grid.SetRow(btnQuiet, 0); Grid.SetColumn(btnQuiet, 2); gridModes.Children.Add(btnQuiet);
            Grid.SetRow(btnBal, 2); Grid.SetColumn(btnBal, 0); gridModes.Children.Add(btnBal);
            Grid.SetRow(btnPerf, 2); Grid.SetColumn(btnPerf, 2); gridModes.Children.Add(btnPerf);
            mainStack.Children.Add(gridModes);


            mainStack.Children.Add(new Rectangle { Height = 1, Fill = Ui.Brush("#25211E"), Margin = new Thickness(0, 0, 0, 8) });


            var fanHead = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            var lblFan = new TextBlock { Text = "Fan Control", FontFamily = Ui.UiFont, FontSize = 11, Foreground = Ui.Brush("#8A8581"), VerticalAlignment = VerticalAlignment.Center };
            txtFanStatus = new TextBlock { Text = "Auto", FontFamily = Ui.MonoFont, FontSize = 10.5, Foreground = Ui.Brush("#A29D99"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            fanHead.Children.Add(lblFan);
            fanHead.Children.Add(txtFanStatus);
            mainStack.Children.Add(fanHead);


            var gridFan = new Grid();
            gridFan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridFan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            gridFan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            btnFanAuto = MakeFanCard("Auto", out txtFanAuto, delegate {
                E.SetFan(FanMode.Auto, E.S.Fan1, E.S.Fan2, false);
                UpdateState();
            });
            btnFanMax = MakeFanCard("Max Fan", out txtFanMax, delegate {
                E.SetFan(FanMode.Max, 0, 0, false);
                UpdateState();
            });
            Grid.SetColumn(btnFanAuto, 0); gridFan.Children.Add(btnFanAuto);
            Grid.SetColumn(btnFanMax, 2); gridFan.Children.Add(btnFanMax);
            mainStack.Children.Add(gridFan);

            rootBox.Child = mainStack;
            Content = rootBox;


            rootBox.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) {
                if (e.LeftButton == MouseButtonState.Pressed && !e.Handled) {
                    try { DragMove(); } catch { }
                    E.S.OverlayPerfX = Left;
                    E.S.OverlayPerfY = Top;
                    E.S.Save();
                }
            };

            SourceInitialized += delegate {
                hwnd = new WindowInteropHelper(this).Handle;
                SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x00000080);
                RestorePosition();
            };

            UpdateState();
        }

        public Action<bool> OnPinnedChanged;

        public void UpdatePinState() {
            Topmost = E.S.OverlayPerfPinned;
            if (pinPath != null) pinPath.Fill = E.S.OverlayPerfPinned ? Ui.Brush(Ui.Accent.Color) : Ui.Brush("#8A8581");
        }

        void SetPerfMode(int idx) {
            E.SetOverlayPerf(idx);
            UpdateState();
        }

        Border MakeModeCard(string label, out TextBlock txt, Action onClick) {
            txt = new TextBlock {
                Text = label,
                FontFamily = Ui.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.Brush("#8A8581"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var b = new Border {
                Background = Ui.Brush("#1C1916"),
                BorderBrush = Ui.Brush("#2B2723"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Height = 32,
                Cursor = Cursors.Hand,
                Child = txt
            };
            OverlayWindow.AttachClick(b, onClick);
            return b;
        }

        Border MakeFanCard(string label, out TextBlock txt, Action onClick) {
            txt = new TextBlock {
                Text = label,
                FontFamily = Ui.UiFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.Brush("#8A8581"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var b = new Border {
                Background = Ui.Brush("#1C1916"),
                BorderBrush = Ui.Brush("#2B2723"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Height = 28,
                Cursor = Cursors.Hand,
                Child = txt
            };
            OverlayWindow.AttachClick(b, onClick);
            return b;
        }

        public void UpdateState() {
            rootBox.Opacity = Math.Max(0.2, Math.Min(1.0, E.S.OverlayOpacity / 100.0));
            Topmost = E.S.OverlayPerfPinned;
            pinPath.Fill = E.S.OverlayPerfPinned ? Ui.Brush(Ui.Accent.Color) : Ui.Brush("#8A8581");

            int m = E.S.OverlayPerfMode;
            SetModeActive(btnEco, txtEco, m == 0, "#2ECC71");
            SetModeActive(btnQuiet, txtQuiet, m == 1, "#00BCD4");
            SetModeActive(btnBal, txtBal, m == 2, "#3498DB");
            SetModeActive(btnPerf, txtPerf, m == 3, "#FF6432");

            bool isMax = E.S.Fan == FanMode.Max;
            SetModeActive(btnFanAuto, txtFanAuto, !isMax, "#3498DB");
            SetModeActive(btnFanMax, txtFanMax, isMax, "#FF6432");

            txtFanStatus.Text = isMax ? "Max Fan" : "Auto Curve";
            txtFanStatus.Foreground = isMax ? Ui.Brush(Ui.Danger) : Ui.Brush("#4AC06C");
        }

        void SetModeActive(Border b, TextBlock t, bool active, string hexColor) {
            if (active) {
                b.Background = Ui.Brush(hexColor);
                b.BorderBrush = Ui.Brush(hexColor);
                t.Foreground = Brushes.White;
            } else {
                b.Background = Ui.Brush("#1C1916");
                b.BorderBrush = Ui.Brush("#2B2723");
                t.Foreground = Ui.Brush("#8A8581");
            }
        }

        public void ResetPosition() {
            var area = SystemParameters.WorkArea;
            Left = area.Left + 30;
            Top = area.Top + 80;
            E.S.OverlayPerfX = Left;
            E.S.OverlayPerfY = Top;
            E.S.Save();
        }

        void RestorePosition() {
            if (E.S.OverlayPerfX >= 0 && E.S.OverlayPerfY >= 0) {
                Left = E.S.OverlayPerfX;
                Top = E.S.OverlayPerfY;
            } else {
                ResetPosition();
            }
            EnsureOnScreen();
        }

        void EnsureOnScreen() {
            var area = SystemParameters.WorkArea;
            if (Left < area.Left || Left > area.Right - 100) Left = area.Left + 30;
            if (Top < area.Top || Top > area.Bottom - 60) Top = area.Top + 80;
        }
    }

    public sealed class OverlayDockWindow : Window {
        readonly Engine E;
        IntPtr hwnd;

        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        readonly Border rootBox;
        readonly Border btnVitals, btnPerf, btnBars, btnFan, btnHotkey, btnDiamond, btnGear, btnClose;
        readonly Path pathVitals, pathPerf, pathBars, pathFan, pathDiamond, pathGear, pathClose;
        readonly TextBlock txtHk1, txtHkPlus, txtHk2;

        public Action OnToggleVitals;
        public Action OnTogglePerf;
        public Action OnToggleBars;
        public Action OnToggleFan;
        public Action OnHotkeyClick;
        public Action OnOpenWholeUi;
        public Action OnOpenSettings;

        const string PATH_GAUGE_MINUS = "M 0 0 M 21 20 M 2.9 16.6 A 7.2 7.2 0 1 1 14.1 16.6 L 12.6 15.3 A 5.3 5.3 0 1 0 4.4 15.3 Z M 5.0 14.8 L 6.2 13.6 L 7.3 14.5 L 6.1 15.7 Z M 4.5 11.2 L 6.3 11.5 L 6.0 12.9 L 4.2 12.6 Z M 7.7 8.0 L 9.3 8.0 L 9.3 9.8 L 7.7 9.8 Z M 10.7 11.5 L 12.5 11.2 L 12.8 12.6 L 11.0 12.9 Z M 10.9 14.5 L 12.0 13.6 L 13.2 14.8 L 12.1 15.7 Z M 8.5 10.5 A 1.5 1.5 0 1 0 8.5 13.5 A 1.5 1.5 0 0 0 8.5 10.5 Z M 7.6 11.2 L 13.8 6.0 L 9.8 13.2 Z M 13.0 1.5 h 6.2 a 1.2 1.2 0 0 1 1.2 1.2 v 0 a 1.2 1.2 0 0 1 -1.2 1.2 h -6.2 a 1.2 1.2 0 0 1 -1.2 -1.2 v 0 a 1.2 1.2 0 0 1 1.2 -1.2 z";
        const string PATH_GAUGE_GRID = "M 0 0 M 21 20 M 2.9 16.6 A 7.2 7.2 0 1 1 14.1 16.6 L 12.6 15.3 A 5.3 5.3 0 1 0 4.4 15.3 Z M 5.0 14.8 L 6.2 13.6 L 7.3 14.5 L 6.1 15.7 Z M 4.5 11.2 L 6.3 11.5 L 6.0 12.9 L 4.2 12.6 Z M 7.7 8.0 L 9.3 8.0 L 9.3 9.8 L 7.7 9.8 Z M 10.7 11.5 L 12.5 11.2 L 12.8 12.6 L 11.0 12.9 Z M 10.9 14.5 L 12.0 13.6 L 13.2 14.8 L 12.1 15.7 Z M 8.5 10.5 A 1.5 1.5 0 1 0 8.5 13.5 A 1.5 1.5 0 0 0 8.5 10.5 Z M 7.6 11.2 L 13.8 6.0 L 9.8 13.2 Z M 13.2 0.8 h 2.8 v 2.8 h -2.8 z M 16.8 0.8 h 2.8 v 2.8 h -2.8 z M 13.2 4.4 h 2.8 v 2.8 h -2.8 z M 16.8 4.4 h 2.8 v 2.8 h -2.8 z";
        const string PATH_BARS = "M0 0 M24 24 M4 19 h2.5 V9 H4 v10 z M8.5 19 h2.5 V4 H8.5 v15 z M13 19 h2.5 V12 H13 v7 z M17.5 19 h2.5 V7 H17.5 v12 z";
        const string PATH_FAN = "M0 0 M24 24 M12 2 A10 10 0 1 0 22 12 A10 10 0 0 0 12 2 Z M12 3.8 A8.2 8.2 0 1 1 3.8 12 A8.2 8.2 0 0 1 12 3.8 Z M12 10.5 A1.5 1.5 0 1 0 12 13.5 A1.5 1.5 0 0 0 12 10.5 Z M12.8 10.6 C14 8 16.8 5.2 18.5 4.5 C17.2 7 15.5 9.5 13.6 11 Z M13.2 13 C15.2 14 17.5 16.8 18 18.5 C15.8 18.2 13 16.8 11.8 14.5 Z M10.5 12.6 C8 13.5 5.2 13.2 4.5 11.5 C7 11.2 9.5 11.5 11 12 Z";
        const string PATH_DIAMOND = "M 0 0 M 20 20 M 10 2.5 L 17.5 10 L 10 17.5 L 2.5 10 Z";
        const string PATH_GEAR = "M0 0 M24 24 M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.488.488 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z";
        const string PATH_CLOSE = "M0 0 M24 24 M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z";

        public OverlayDockWindow(Engine engine) {
            E = engine;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            rootBox = new Border {
                Background = Ui.Brush("#EB181614"),
                BorderBrush = Ui.Brush("#36312B"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 8, 6, 8),
                Width = 46,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.65, Color = Colors.Black }
            };

            var stack = new StackPanel();


            btnVitals = MakeDockButton(PATH_GAUGE_MINUS, "RAM & FPS System Vitals", out pathVitals, () => {
                if (OnToggleVitals != null) OnToggleVitals();
            }, 18);
            stack.Children.Add(btnVitals);


            btnPerf = MakeDockButton(PATH_GAUGE_GRID, "Performance Control", out pathPerf, () => {
                if (OnTogglePerf != null) OnTogglePerf();
            }, 18);
            stack.Children.Add(btnPerf);


            btnBars = MakeDockButton(PATH_BARS, "Vitals Detail & Layout", out pathBars, () => {
                if (OnToggleBars != null) OnToggleBars();
            }, 17);
            stack.Children.Add(btnBars);


            btnFan = MakeDockButton(PATH_FAN, "Toggle Max Fan", out pathFan, () => {
                if (OnToggleFan != null) OnToggleFan();
            }, 17);
            stack.Children.Add(btnFan);


            stack.Children.Add(MakeDivider());


            btnHotkey = new Border {
                Background = Ui.Brush("#1C1917"),
                BorderBrush = Ui.Brush("#2E2925"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(2, 4, 2, 4),
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = Cursors.Hand,
                ToolTip = "Toggle Overlay Shortcut (Click to toggle)"
            };
            var hkStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            txtHk1 = new TextBlock { Text = "Shift", FontFamily = Ui.UiFont, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#C0BAB4"), HorizontalAlignment = HorizontalAlignment.Center };
            txtHkPlus = new TextBlock { Text = "+", FontFamily = Ui.UiFont, FontSize = 9, Foreground = Ui.Brush("#7A7570"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -1, 0, -1) };
            txtHk2 = new TextBlock { Text = "F2", FontFamily = Ui.UiFont, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Ui.Brush("#EDEAE8"), HorizontalAlignment = HorizontalAlignment.Center };
            hkStack.Children.Add(txtHk1);
            hkStack.Children.Add(txtHkPlus);
            hkStack.Children.Add(txtHk2);
            btnHotkey.Child = hkStack;
            OverlayWindow.AttachClick(btnHotkey, () => {
                if (OnHotkeyClick != null) OnHotkeyClick();
            });
            stack.Children.Add(btnHotkey);


            btnDiamond = MakeDockButton(PATH_DIAMOND, "Open Seal Performance Dashboard", out pathDiamond, () => {
                if (OnOpenWholeUi != null) OnOpenWholeUi();
            }, 17);
            pathDiamond.Fill = Ui.Brush(Ui.Accent.Color);
            stack.Children.Add(btnDiamond);


            btnGear = MakeIconButton(PATH_GEAR, "Open Overlay Settings", out pathGear, () => {
                if (OnOpenSettings != null) OnOpenSettings();
            });
            stack.Children.Add(btnGear);


            stack.Children.Add(MakeDivider());


            btnClose = MakeIconButton(PATH_CLOSE, "Close Dock", out pathClose, () => {
                Hide();
            });
            stack.Children.Add(btnClose);

            rootBox.Child = stack;
            Content = rootBox;


            rootBox.MouseLeftButtonDown += (s, e) => {
                if (e.LeftButton == MouseButtonState.Pressed && !e.Handled) {
                    try { DragMove(); } catch { }
                    E.S.OverlayDockX = Left;
                    E.S.OverlayDockY = Top;
                    E.S.Save();
                }
            };

            SourceInitialized += delegate {
                hwnd = new WindowInteropHelper(this).Handle;
                SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x00000080);
                RestorePosition();
            };

            UpdateHotkeyDisplay();
        }

        Border MakeDockButton(string pathData, string tip, out Path p, Action onClick, double iconSize = 18) {
            p = new Path {
                Data = Geometry.Parse(pathData),
                Width = iconSize,
                Height = iconSize,
                Stretch = Stretch.Uniform,
                Fill = Ui.Brush("#C0BAB4"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var b = new Border {
                Width = 34,
                Height = 34,
                Background = Ui.Brush("#201C19"),
                BorderBrush = Ui.Brush("#2B2723"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                ToolTip = tip,
                Child = p
            };
            OverlayWindow.AttachClick(b, onClick);
            return b;
        }

        Border MakeIconButton(string pathData, string tip, out Path p, Action onClick) {
            p = new Path {
                Data = Geometry.Parse(pathData),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                Fill = Ui.Brush("#8A8581"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var b = new Border {
                Width = 34,
                Height = 26,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand,
                ToolTip = tip,
                Child = p
            };
            OverlayWindow.AttachClick(b, onClick);
            return b;
        }

        Rectangle MakeDivider() {
            return new Rectangle {
                Height = 1,
                Fill = Ui.Brush("#2A2522"),
                Margin = new Thickness(4, 5, 4, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        public void UpdateHotkeyDisplay() {
            string hk = string.IsNullOrEmpty(E.S.OverlayHotkey) ? "Shift+F2" : E.S.OverlayHotkey;
            if (btnHotkey != null) btnHotkey.ToolTip = "Toggle Overlay (" + hk + ")";
            string[] parts = hk.Split('+');
            if (parts.Length >= 2) {
                txtHk1.Text = parts[0];
                txtHkPlus.Visibility = Visibility.Visible;
                txtHk2.Text = parts[parts.Length - 1];
            } else {
                txtHk1.Text = hk;
                txtHkPlus.Visibility = Visibility.Collapsed;
                txtHk2.Text = "";
            }
        }

        public void SetState(bool vitalsOpen, bool perfOpen, bool isMaxFan, bool mainUiOpen = false) {
            SetButtonActive(btnVitals, pathVitals, vitalsOpen);
            SetButtonActive(btnPerf, pathPerf, perfOpen);
            SetButtonActive(btnFan, pathFan, isMaxFan, Ui.Danger);
            SetButtonActive(btnDiamond, pathDiamond, mainUiOpen, Ui.Accent.Color, Ui.Accent.Color);
            UpdateHotkeyDisplay();
        }

        void SetButtonActive(Border b, Path p, bool active, Color? customColor = null, Color? defaultColor = null) {
            var col = customColor ?? Ui.Accent.Color;
            if (active) {
                b.Background = Ui.Brush("#2A221C");
                b.BorderBrush = Ui.Brush(col);
                p.Fill = Ui.Brush(col);
            } else {
                b.Background = Ui.Brush("#201C19");
                b.BorderBrush = Ui.Brush("#2B2723");
                p.Fill = defaultColor.HasValue ? Ui.Brush(defaultColor.Value) : Ui.Brush("#C0BAB4");
            }
        }

        public void ResetPosition() {
            var area = SystemParameters.WorkArea;
            Left = area.Left + 16;
            Top = area.Top + (area.Height - 320) / 2;
            E.S.OverlayDockX = Left;
            E.S.OverlayDockY = Top;
            E.S.Save();
        }

        void RestorePosition() {
            if (E.S.OverlayDockX >= 0 && E.S.OverlayDockY >= 0) {
                Left = E.S.OverlayDockX;
                Top = E.S.OverlayDockY;
            } else {
                ResetPosition();
            }
        }
    }
}
