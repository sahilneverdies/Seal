
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WPath = System.Windows.Shapes.Path;
using WF = System.Windows.Forms;
using SD = System.Drawing;

namespace Seal {


    public sealed class MainWindow : Window {
        enum Page { Home = 0, Fans = 1, Memory = 2, Overlay = 3, Keyboard = 4, Settings = 5 }
        const double RailW = 62, PageW = 398, KbdPageW = 638, SettingsH = 640, MemoryH = 520, OverlayH = 580;

        Osd osd;
        public void Flash(string title, string detail, int modeIndex) {
            if (osd == null) osd = new Osd();
            string[] paths = { LEAF, SCALE, BOLT };
            osd.Flash(title, detail, modeIndex >= 0 ? Ui.ModeColor(modeIndex) : Ui.Accent.Color, modeIndex >= 0 ? paths[modeIndex] : FAN);
        }
        const string FAN = "M12 12 C9.6 8.4 8.4 5 10.3 2 C14.6 2.4 15.9 6.6 12 9 M12 12 C15.3 13.2 17.6 16.2 16.6 19.6 C12.5 20.6 9.4 17.6 12 15 M12 12 C11.1 15.4 8 17.8 4.6 16.4 C4 12.2 7.2 9.8 12 9";
        const string LEAF = "M4 20 C4 11 10 4 20 4 C20 13 14 20 4 20 Z M4 20 L13 11";
        const string SCALE = "M12 3 L12 21 M8 21 L16 21 M4 7 L20 7 M4 7 L1.5 13 A2.5 2 0 0 0 6.5 13 Z M20 7 L17.5 13 A2.5 2 0 0 0 22.5 13 Z";
        const string BOLT = "M13 2 L4 14 L11 14 L10 22 L20 9 L13 9 Z";
        const string ICO_HOME_RING = "M12 3.5 A8.5 8.5 0 1 0 12 20.5 A8.5 8.5 0 1 0 12 3.5 Z";
        const string ICO_HOME_DOT = "M12 8.6 A3.4 3.4 0 1 0 12 15.4 A3.4 3.4 0 1 0 12 8.6 Z";
        const string ICO_UPDATE = "M12 4 V14.2 M7.6 10.2 L12 14.8 L16.4 10.2 M4.6 19 H19.4";
        const string ICO_FANS = "M3 8.5 C5.5 5.5 8.5 11.5 12 8.5 C15.5 5.5 18.5 11.5 21 8.5 M3 15.5 C5.5 12.5 8.5 18.5 12 15.5 C15.5 12.5 18.5 18.5 21 15.5";
        const string ICO_MEM_STROKE = "M3 7 A1.5 1.5 0 0 1 4.5 5.5 H19.5 A1.5 1.5 0 0 1 21 7 V17 A1.5 1.5 0 0 1 19.5 18.5 H14 L13 16.5 H11 L10 18.5 H4.5 A1.5 1.5 0 0 1 3 17 Z M3 14.5 H21";
        const string ICO_MEM_CHIPS = "M5.5 8 H8.5 V12 H5.5 Z M10.5 8 H13.5 V12 H10.5 Z M15.5 8 H18.5 V12 H15.5 Z";
        const string ICO_OVERLAY = "M12 4 C7.03 4 3 8.03 3 13 C3 15.5 4.02 17.76 5.67 19.38 L7.08 17.97 C5.79 16.69 5 14.94 5 13 C5 9.13 8.13 6 12 6 C15.87 6 19 9.13 19 13 C19 14.94 18.21 16.69 16.92 17.97 L18.33 19.38 C19.98 17.76 21 15.5 21 13 C21 8.03 16.97 4 12 4 Z M12 8 C11.45 8 11 8.45 11 9 L11 12.59 L8.71 14.88 L10.12 16.29 L12.71 13.71 C12.89 13.53 13 13.28 13 13 C13 12.45 12.55 12 12 12 L12 9 C12 8.45 11.55 8 12 8 Z";
        const string ICO_KBD = "M2.5 7.5 A2 2 0 0 1 4.5 5.5 H19.5 A2 2 0 0 1 21.5 7.5 V16.5 A2 2 0 0 1 19.5 18.5 H4.5 A2 2 0 0 1 2.5 16.5 Z M6 9.5 H6.4 M9.8 9.5 H10.2 M13.6 9.5 H14 M17.4 9.5 H17.8 M6 12.5 H6.4 M9.8 12.5 H10.2 M13.6 12.5 H14 M17.4 12.5 H17.8 M7.5 15.5 H16.5";

        readonly Engine E;
        readonly Sensors sensors;
        readonly string screenshotPath;
        readonly bool openSettings;
        FrameworkElement root;
        Grid pageHost;
        ScrollViewer scroll;
        readonly FrameworkElement[] pages = new FrameworkElement[6];
        Page cur = Page.Home;
        bool pageShown;
        readonly NavBtn[] nav = new NavBtn[6];
        Border railPill;
        TranslateTransform railPillT;
        Canvas railCanvas;
        FrameworkElement rail, logoHost;
        StackPanel navBottom;
        ColorSource accentSrc;
        SolidColorBrush accent;

        TextBlock txtHomeTitle, txtHomeStatus, subCpu, subGpu, txtSettingsPowerVal, txtFoot, txtFootRight, txtLightSub, txtErr, txtInfo, btnSupport, btnReset;
        Run bigCpu, bigGpu, bigFan1, bigFan2;
        FrameworkElement demoBadge, errBanner, infoBanner, lightRow, settingsPowerGainRow;
        Border miniHost, infoClose;
        Ellipse dotHb;
        Seg modeSeg;
        LinkSeg fanLinks;
        Slider slSettingsPower;

        Seg fanSeg, stopAfterSeg;
        TextBlock txtFansStatus, txtCurveTitle, txtCurveHint, txtFan1, txtFan2, txtFanApplied, txtFanRight, btnFanAction, txtFloor, txtRamp, txtGuardNote;
        TextBlock maxFan1Sub, maxFan2Sub, maxTempSub, maxMinsSub, manSub1, manSub2;
        Run maxFan1, maxFan2, maxTemp, maxMins, maxMinsUnit, manPct1, manPct2;
        FrameworkElement curveBlock, maxBlock, manualBlock, optsAuto, optsCurve, optsMax, optsManual;
        Border curveWhichHost;
        LinkSeg curveWhich;
        CurveView curveView;
        Slider slFan1, slFan2, slFloor, slRamp;
        ToggleButton tgLink, tgEcoCool2, tgMaxCool, tgManualLink;
        bool curveGpu;

        ScrollViewer scrollMemory;
        double scrollMemoryTo;
        bool scrollingMemory;
        TextBlock txtMemTotal, txtMemUsedPct, txtMemFreePct, txtOptimizeBtn, txtMemStatus;
        Run txtMemUsed, txtMemFree;
        Border memBarUsed, btnOptimizeMem;
        ToggleButton tgMemWorkingSet, tgMemSystemCache, tgMemStandby, tgMemModified, tgMemCombined, tgMemRegistry, tgMemNotify;
        Seg memIntervalSeg, memThresholdSeg;
        DateTime lastMemAutoClean = DateTime.MinValue;
        DateTime lastMemThresholdClean = DateTime.MinValue;
        bool optimizingMem;

        OverlayWindow overlayWin;
        OverlayPerfWindow overlayPerfWin;
        OverlayDockWindow overlayDockWin;
        ScrollViewer scrollOverlay;
        TextBlock txtOverlayHeadStatus, txtOverlayOpacity;
        ToggleButton tgOverlayEnable, tgOverlayDock, tgOverlayPin, tgOverlayCpu, tgOverlayGpu, tgOverlayRam, tgOverlayFps, tgOverlayUpload, tgOverlayDownload, tgOverlayPerf;
        Slider slOverlayOpacity;
        Seg overlayPerfSeg, overlayOrientSeg;
        Border btnOpenOverlay, btnResetOverlayPos, cardOverlayEnable, btnOverlayHotkey;
        TextBlock txtOverlayHotkey;
        DispatcherTimer tempDismissTimer;
        Window tempWin1, tempWin2;
        bool overlayHotkeyRegistered;
        bool isRecordingOverlayHotkey;
        const int HOTKEY_OVERLAY = 100;

        LinkSeg kbdModes;
        ChipSeg extAnimSeg;
        Seg granSeg;
        Border kbdHost, hexChip;
        TextBlock txtKbdStatus, txtKeySel, txtSpeed, txtLevel, txtLevel2, txtKbdInfo, btnWinLighting;
        TextBlock txtExtDeviceName, txtExtDeviceDetail, btnRefreshExt, txtExtSpeed, txtExtLevel, txtExtBrandTips;
        Ellipse extStatusDot;
        FrameworkElement selectRow, colorEditor, effectEditor, kbdInfo, levelInline, externalKbdEditor;
        Slider slSpeed, slLevel, slLevel2, slExtSpeed, slExtLevel;
        TextBox txtHex;
        StripPicker hueBar, shadeBar;
        KeyboardView kbdMini, kbdBig;
        DispatcherTimer colorDebounce, levelDebounce, speedDebounce, floorDebounce, extSpeedDebounce, extLevelDebounce;
        bool miniNeedsFrame;
        string gran = "Zone";
        Rgb curColor;
        bool hexTyping;

        Seg keySeg, gfxSeg, hzSeg, gpuSeg, pollSeg;
        TextBlock txtMachine, txtKeyInfo, txtGfxSub, txtGpuSub, txtDiag, txtUpdate, txtUpdateTitle, btnLearn, btnUpdate, btnDiag, btnLog, btnExit;

        TextBlock txtDriverTitle, txtDriverSub, btnDriver, txtDriverNudge, txtDriverNudgeSub, btnDriverNudge;
        ToggleButton tgDriver;
        FrameworkElement driverBanner, driverClose, driverRow;
        Run runCpuHead, runCpuWatts, runCpuTail;
        string cpuLimitsTip = "?";
        bool cpuTipFromDriver;
        enum DriverState { Busy, NotInstalled, RestartPending, Off, Outdated, Ready, Broken }
        DriverState driverState = DriverState.NotInstalled;
        Brush subCpuBrush;
        string driverRowFor = "?";
        Border updateRow;
        string updateTitleFor = "?";
        StackPanel updateText;
        NavBtn navUpdate;
        FrameworkElement keyCmdRow, gfxRow, hzRow, lowHzRow, gpuRow, kbdLightingRow;
        TextBox txtKeyCmd;
        Ellipse keyDot;
        ToggleButton tgSuppress, tgHotkeys, tgKbdLighting, tgAutostart, tgEcoBattery, tgSyncPower, tgLowHzBattery, tgTrayTemp, tgGuard, tgUpdateAuto;

        Button btnHotkeys;
        TextBlock txtHotkeysSub;
        StackPanel hotkeyPanel;
        bool hotkeysOpen;
        int listening = -1;
        readonly bool[] hotkeyBusy = new bool[HotkeyTable.Count];
        string hotkeySubFor = "?";
        TextBlock listeningCap;

        WrapPanel guardLine;
        Button btnGuard;
        bool guardOpen;
        ValueLink chipCpu, chipChassis, chipFans, chipHold;
        int cpuLo = 70, chassisLo = 40;
        static readonly int[] HoldChoices = { 30, 60, 120, 180, 300 };
        int[] holdChoices = HoldChoices;
        int[] guardLevels = new int[0];
        DispatcherTimer guardDebounce;
        int lastChassis = -1, cpuTipFor = -1, chassisTipFor = -1;
        TextBlock txtGuardSub, txtMaxCoolSub, txtKeyCmdHint;
        Button btnClose;
        Border toast;
        TextBlock txtToast;
        readonly List<WF.ToolStripMenuItem> trayHz = new List<WF.ToolStripMenuItem>();
        WF.NotifyIcon tray;
        readonly WF.ToolStripMenuItem[] trayModes = new WF.ToolStripMenuItem[3];
        readonly SD.Icon[] icons = new SD.Icon[3];
        readonly BitmapSource[] appIcons = new BitmapSource[3];
        bool syncing, exiting, reading, autostart;
        int shownMode = -1, lastBiosTemp = -1;
        int[] lastFans;
        readonly List<double> tempTrail = new List<double>();
        DispatcherTimer uiTimer, fanDebounce, powerDebounce, curveDebounce, learnTimer, toastTimer;
        HwndSource src;
        IntPtr hwnd;
        bool hotkeysRegistered;
        bool onBattery;

        static readonly string[] ModeSubs = {
            "Windows efficiency mode · GPU base power",
            "Default thermal policy · GPU boost",
            "Performance thermal policy · GPU max"
        };

        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, VK_F11 = 0x7A;
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        const uint MOD_NOREPEAT = 0x4000;
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public MainWindow(Engine engine, Sensors s, string screenshot, bool settingsOpen) {
            E = engine;
            sensors = s;
            screenshotPath = screenshot;
            openSettings = settingsOpen;
            Ui.LoadFonts();
            Title = Program.DisplayName;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanMinimize;
            Background = Ui.Card;
            Width = RailW + PageW;
            Height = 600;
            SizeToContent = SizeToContent.Manual;
            ShowInTaskbar = true;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            root = LoadXaml();
            Content = root;
            root.Resources["UiFont"] = Ui.UiFont;
            root.Resources["MonoFont"] = Ui.MonoFont;

            accentSrc = new ColorSource { Color = Ui.ModeColor(E.ModeIndex) }; accent = accentSrc.MakeBrush();
            root.Resources["Accent"] = accent;
            Ui.Accent = accent;
            FindAll();
            Bounds();
            BuildRail();
            BuildHome();
            BuildFans();
            BuildMemory();
            BuildOverlay();
            BuildKeyboard();
            BuildSettings();
            BuildIcons();
            BuildTray();
            Wire();
            Position();
            GuardText();
            if (!E.P.HasPowerGain && settingsPowerGainRow != null) {
                settingsPowerGainRow.Visibility = Visibility.Collapsed;
            }
            SetAccent(E.ModeIndex, false);
            Navigate(Page.Home, false);
            root.Measure(new Size(Width, double.PositiveInfinity));
            if (pageHost.DesiredSize.Height > 100) Height = Math.Ceiling(pageHost.DesiredSize.Height);

            pageHost.SizeChanged += delegate { Morph(true); };

            E.StateChanged += delegate { Dispatcher.BeginInvoke((Action)Refresh); };
            E.Toast += delegate(string m, bool err) { Dispatcher.BeginInvoke((Action)delegate { ShowToast(m, err); }); };
            E.KeyPressed += delegate(KeyAction a) { Dispatcher.BeginInvoke((Action)delegate { OnKey(a); }); };
            E.AnyKeyEvent += delegate(uint id, uint data) { Dispatcher.BeginInvoke((Action)delegate { if (!E.Learning) UpdateKeyStatus(); }); };
            sensors.Updated += delegate(SensorSnapshot snap) { Dispatcher.BeginInvoke((Action)delegate { OnSensors(snap); }); };
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerMode;

            SourceInitialized += OnSourceInit;
            Loaded += OnLoaded;
            Closing += delegate(object o, System.ComponentModel.CancelEventArgs ce) { if (!exiting) { ce.Cancel = true; HideToTray(); } };
            Application.Current.SessionEnding += delegate { quietExit = true; ExitApp(); };
            StateChanged += delegate { if (WindowState == WindowState.Minimized) { WindowState = WindowState.Normal; HideToTray(); } };
            IsVisibleChanged += delegate { PollRate(); if (IsVisible) ReadHardwareAsync(); };
            LocationChanged += delegate { if (IsVisible && WindowState == WindowState.Normal && Left > -30000 && !morphing) { E.S.WinX = (int)Left; E.S.WinY = (int)Top; } };
            KeyDown += delegate(object o, KeyEventArgs ke) { if (ke.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox)) HideToTray(); };
            MouseEnter += delegate { Ui.Fade(btnClose, 1, 140); };
            MouseLeave += delegate { Ui.Fade(btnClose, 0, 200); };


            uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            uiTimer.Tick += delegate {
                ReadHardwareAsync();
                UpdateFooter();
                if (cur == Page.Fans && E.S.Fan == FanMode.Max) UpdateMaxBlock();
                if (cur == Page.Memory) UpdateMemoryLive();
                CheckAutoMemoryClean();
            };
            PollRate();
            Refresh();
            if (!E.BiosOk && !E.Hw.IsDemo) ShowToast("BIOS interface unavailable: " + E.LastError, true);
            else if (E.LastError.Length > 0) ShowToast(E.LastError, true);
            QueryAutostartAsync();
            if (!E.Hw.IsDemo && E.S.UpdateOnLaunch) Slow(delegate { E.CheckForUpdate(false); });
            StartShowListener();

            new WindowInteropHelper(this).EnsureHandle();
        }


        static FrameworkElement LoadXaml() {
            using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Seal.Ui.xaml")) {
                if (st == null) throw new InvalidOperationException("embedded Ui.xaml missing");
                return (FrameworkElement)XamlReader.Load(st);
            }
        }
        T F<T>(string name) where T : class {
            var o = root.FindName(name) as T;
            if (o == null) throw new InvalidOperationException("XAML element missing: " + name);
            return o;
        }
        void FindAll() {
            rail = F<FrameworkElement>("Rail");
            railCanvas = F<Canvas>("RailCanvas");
            railPill = F<Border>("RailPill");
            railPillT = (TranslateTransform)railPill.RenderTransform;
            logoHost = F<FrameworkElement>("LogoHost");
            var markBorder = root.FindName("Mark") as Border;
            if (markBorder != null) markBorder.Background = accentSrc.MakeGradient();
            var logoImg = root.FindName("LogoImg") as Image;
            if (logoImg != null) logoImg.Source = GetMarkImageSource();
            scroll = F<ScrollViewer>("Scroll");
            pageHost = F<Grid>("PageHost");
            pages[0] = F<FrameworkElement>("PageHome");
            pages[1] = F<FrameworkElement>("PageFans");
            pages[2] = F<FrameworkElement>("PageMemory");
            pages[3] = F<FrameworkElement>("PageOverlay");
            pages[4] = F<FrameworkElement>("PageKbd");
            pages[5] = F<FrameworkElement>("PageSettings");
            scrollOverlay = F<ScrollViewer>("ScrollOverlay");
            txtOverlayHeadStatus = F<TextBlock>("TxtOverlayHeadStatus");
            tgOverlayEnable = F<ToggleButton>("TgOverlayEnable");
            tgOverlayPin = F<ToggleButton>("TgOverlayPin");
            slOverlayOpacity = F<Slider>("SlOverlayOpacity");
            txtOverlayOpacity = F<TextBlock>("TxtOverlayOpacity");
            tgOverlayCpu = F<ToggleButton>("TgOverlayCpu");
            tgOverlayGpu = F<ToggleButton>("TgOverlayGpu");
            tgOverlayRam = F<ToggleButton>("TgOverlayRam");
            tgOverlayFps = F<ToggleButton>("TgOverlayFps");
            tgOverlayUpload = F<ToggleButton>("TgOverlayUpload");
            tgOverlayDownload = F<ToggleButton>("TgOverlayDownload");
            tgOverlayPerf = F<ToggleButton>("TgOverlayPerf");
            tgOverlayDock = F<ToggleButton>("TgOverlayDock");
            btnOpenOverlay = F<Border>("BtnOpenOverlay");
            btnResetOverlayPos = F<Border>("BtnResetOverlayPos");
            cardOverlayEnable = F<Border>("CardOverlayEnable");
            btnOverlayHotkey = F<Border>("BtnOverlayHotkey");
            txtOverlayHotkey = F<TextBlock>("TxtOverlayHotkey");
            scrollMemory = F<ScrollViewer>("ScrollMemory");
            txtMemTotal = F<TextBlock>("TxtMemTotal");
            txtMemUsed = F<Run>("TxtMemUsed");
            txtMemUsedPct = F<TextBlock>("TxtMemUsedPct");
            txtMemFree = F<Run>("TxtMemFree");
            txtMemFreePct = F<TextBlock>("TxtMemFreePct");
            memBarUsed = F<Border>("MemBarUsed");
            btnOptimizeMem = F<Border>("BtnOptimizeMem");
            txtOptimizeBtn = F<TextBlock>("TxtOptimizeBtn");
            txtMemStatus = F<TextBlock>("TxtMemStatus");
            tgMemWorkingSet = F<ToggleButton>("TgMemWorkingSet");
            tgMemSystemCache = F<ToggleButton>("TgMemSystemCache");
            tgMemStandby = F<ToggleButton>("TgMemStandby");
            tgMemModified = F<ToggleButton>("TgMemModified");
            tgMemCombined = F<ToggleButton>("TgMemCombined");
            tgMemRegistry = F<ToggleButton>("TgMemRegistry");
            tgMemNotify = F<ToggleButton>("TgMemNotify");
            txtHomeTitle = F<TextBlock>("TxtHomeTitle");
            txtHomeStatus = F<TextBlock>("TxtHomeStatus");
            demoBadge = F<FrameworkElement>("DemoBadge");
            errBanner = F<FrameworkElement>("ErrBanner");
            txtErr = F<TextBlock>("TxtErr");
            infoBanner = F<FrameworkElement>("InfoBanner");
            txtInfo = F<TextBlock>("TxtInfo");
            infoClose = F<Border>("InfoClose");
            infoClose.MouseLeftButtonUp += delegate {
                E.S.InfoDismissed = true; try { E.S.Save(); } catch { }
                infoBanner.Visibility = Visibility.Collapsed;
                Remeasure(cur);
            };
            bigCpu = F<Run>("BigCpu");
            bigGpu = F<Run>("BigGpu");
            subCpu = F<TextBlock>("SubCpu");
            subGpu = F<TextBlock>("SubGpu");
            bigFan1 = F<Run>("BigFan1");
            bigFan2 = F<Run>("BigFan2");
            settingsPowerGainRow = F<FrameworkElement>("SettingsPowerGainRow");
            slSettingsPower = F<Slider>("SlSettingsPower");
            txtSettingsPowerVal = F<TextBlock>("TxtSettingsPowerVal");
            lightRow = F<FrameworkElement>("LightRow");
            txtLightSub = F<TextBlock>("TxtLightSub");
            miniHost = F<Border>("MiniHost");
            dotHb = F<Ellipse>("DotHb");
            txtFoot = F<TextBlock>("TxtFoot");
            txtFootRight = F<TextBlock>("TxtFootRight");
            txtFansStatus = F<TextBlock>("TxtFansStatus");
            curveBlock = F<FrameworkElement>("CurveBlock");
            txtCurveTitle = F<TextBlock>("TxtCurveTitle");
            curveWhichHost = F<Border>("CurveWhichHost");
            txtCurveHint = F<TextBlock>("TxtCurveHint");
            maxBlock = F<FrameworkElement>("MaxBlock");
            maxFan1 = F<Run>("MaxFan1");
            maxFan1Sub = F<TextBlock>("MaxFan1Sub");
            maxFan2 = F<Run>("MaxFan2");
            maxFan2Sub = F<TextBlock>("MaxFan2Sub");
            maxTemp = F<Run>("MaxTemp");
            maxTempSub = F<TextBlock>("MaxTempSub");
            maxMins = F<Run>("MaxMins");
            maxMinsUnit = F<Run>("MaxMinsUnit");
            maxMinsSub = F<TextBlock>("MaxMinsSub");
            manualBlock = F<FrameworkElement>("ManualBlock");
            manPct1 = F<Run>("ManPct1");
            manSub1 = F<TextBlock>("ManSub1");
            manPct2 = F<Run>("ManPct2");
            manSub2 = F<TextBlock>("ManSub2");
            optsAuto = F<FrameworkElement>("OptsAuto");
            tgEcoCool2 = F<ToggleButton>("TgEcoCool2");
            optsCurve = F<FrameworkElement>("OptsCurve");
            tgLink = F<ToggleButton>("TgLink");
            slFloor = F<Slider>("SlFloor");
            txtFloor = F<TextBlock>("TxtFloor");
            slRamp = F<Slider>("SlRamp");
            txtRamp = F<TextBlock>("TxtRamp");
            optsMax = F<FrameworkElement>("OptsMax");
            tgMaxCool = F<ToggleButton>("TgMaxCool");
            optsManual = F<FrameworkElement>("OptsManual");
            slFan1 = F<Slider>("SlFan1");
            slFan2 = F<Slider>("SlFan2");
            txtFan1 = F<TextBlock>("TxtFan1");
            txtFan2 = F<TextBlock>("TxtFan2");
            tgManualLink = F<ToggleButton>("TgManualLink");
            txtGuardNote = F<TextBlock>("TxtGuardNote");
            txtFanApplied = F<TextBlock>("TxtFanApplied");
            txtFanRight = F<TextBlock>("TxtFanRight");
            btnFanAction = F<TextBlock>("BtnFanAction");
            txtKbdStatus = F<TextBlock>("TxtKbdStatus");
            selectRow = F<FrameworkElement>("SelectRow");
            txtKeySel = F<TextBlock>("TxtKeySel");
            kbdHost = F<Border>("KbdHost");
            colorEditor = F<FrameworkElement>("ColorEditor");
            effectEditor = F<FrameworkElement>("EffectEditor");
            kbdInfo = F<FrameworkElement>("KbdInfo");
            hexChip = F<Border>("HexChip");
            txtHex = F<TextBox>("TxtHex");
            slLevel = F<Slider>("SlLevel");
            txtLevel = F<TextBlock>("TxtLevel");
            levelInline = F<FrameworkElement>("LevelInline");
            slSpeed = F<Slider>("SlSpeed");
            txtSpeed = F<TextBlock>("TxtSpeed");
            slLevel2 = F<Slider>("SlLevel2");
            txtLevel2 = F<TextBlock>("TxtLevel2");
            txtKbdInfo = F<TextBlock>("TxtKbdInfo");
            btnWinLighting = F<TextBlock>("BtnWinLighting");
            externalKbdEditor = F<FrameworkElement>("ExternalKbdEditor");
            txtExtDeviceName = F<TextBlock>("TxtExtDeviceName");
            txtExtDeviceDetail = F<TextBlock>("TxtExtDeviceDetail");
            btnRefreshExt = F<TextBlock>("BtnRefreshExt");
            txtExtSpeed = F<TextBlock>("TxtExtSpeed");
            txtExtLevel = F<TextBlock>("TxtExtLevel");
            txtExtBrandTips = F<TextBlock>("TxtExtBrandTips");
            extStatusDot = F<Ellipse>("ExtStatusDot");
            slExtSpeed = F<Slider>("SlExtSpeed");
            slExtLevel = F<Slider>("SlExtLevel");
            txtMachine = F<TextBlock>("TxtMachine");
            txtKeyInfo = F<TextBlock>("TxtKeyInfo");
            keyDot = F<Ellipse>("KeyDot");
            btnLearn = F<TextBlock>("BtnLearn");
            keyCmdRow = F<FrameworkElement>("KeyCmdRow");
            txtKeyCmd = F<TextBox>("TxtKeyCmd");
            gfxRow = F<FrameworkElement>("GfxRow");
            txtGfxSub = F<TextBlock>("TxtGfxSub");
            hzRow = F<FrameworkElement>("HzRow");
            lowHzRow = F<FrameworkElement>("LowHzRow");
            gpuRow = F<FrameworkElement>("GpuRow");
            txtGpuSub = F<TextBlock>("TxtGpuSub");
            tgSuppress = F<ToggleButton>("TgSuppress");
            tgHotkeys = F<ToggleButton>("TgHotkeys");
            btnHotkeys = F<Button>("BtnHotkeys");
            txtHotkeysSub = F<TextBlock>("TxtHotkeysSub");
            hotkeyPanel = F<StackPanel>("HotkeyPanel");
            kbdLightingRow = F<FrameworkElement>("KbdLightingRow");
            tgKbdLighting = F<ToggleButton>("TgKbdLighting");
            guardLine = F<WrapPanel>("GuardLine");
            btnGuard = F<Button>("BtnGuard");
            tgEcoBattery = F<ToggleButton>("TgEcoBattery");
            tgLowHzBattery = F<ToggleButton>("TgLowHzBattery");
            tgSyncPower = F<ToggleButton>("TgSyncPower");
            tgTrayTemp = F<ToggleButton>("TgTrayTemp");
            tgAutostart = F<ToggleButton>("TgAutostart");
            tgGuard = F<ToggleButton>("TgGuard");
            tgUpdateAuto = F<ToggleButton>("TgUpdateAuto");
            txtGuardSub = F<TextBlock>("TxtGuardSub");
            txtMaxCoolSub = F<TextBlock>("TxtMaxCoolSub");
            txtKeyCmdHint = F<TextBlock>("TxtKeyCmdHint");
            txtUpdate = F<TextBlock>("TxtUpdate");
            btnUpdate = F<TextBlock>("BtnUpdate");

            txtUpdateTitle = F<TextBlock>("TxtUpdateTitle");
            updateRow = F<Border>("UpdateRow");
            updateText = F<StackPanel>("UpdateText");
            btnDiag = F<TextBlock>("BtnDiag");
            btnSupport = F<TextBlock>("BtnSupport");
            btnReset = F<TextBlock>("BtnReset");
            btnLog = F<TextBlock>("BtnLog");
            btnExit = F<TextBlock>("BtnExit");
            txtDiag = F<TextBlock>("TxtDiag");
            txtDriverTitle = F<TextBlock>("TxtDriverTitle");
            txtDriverSub = F<TextBlock>("TxtDriverSub");
            btnDriver = F<TextBlock>("BtnDriver");
            tgDriver = F<ToggleButton>("TgDriver");
            driverBanner = F<FrameworkElement>("DriverBanner");
            txtDriverNudge = F<TextBlock>("TxtDriverNudge");
            txtDriverNudgeSub = F<TextBlock>("TxtDriverNudgeSub");
            btnDriverNudge = F<TextBlock>("BtnDriverNudge");
            driverClose = F<FrameworkElement>("DriverClose");
            driverRow = F<FrameworkElement>("DriverRow");
            subCpuBrush = subCpu.Foreground;
            btnClose = F<Button>("BtnClose");
            toast = F<Border>("Toast");
            txtToast = F<TextBlock>("TxtToast");
            btnExit.Text = "Exit " + Program.DisplayName;
            foreach (string n in new[] { "SecKey", "SecPower", "SecDisplay", "SecDriver", "SecApp" }) Track(n);
        }

        void Track(string name) {
            var tb = F<TextBlock>(name);
            var parent = tb.Parent as Panel;
            if (parent == null) return;
            int i = parent.Children.IndexOf(tb);
            var t = new Tracked { Text = tb.Text, Margin = tb.Margin, HorizontalAlignment = HorizontalAlignment.Left };
            parent.Children.RemoveAt(i);
            parent.Children.Insert(i, t);
        }

        void Bounds() {
            slFan1.Minimum = slFan2.Minimum = 0;
            slFan1.Maximum = slFan2.Maximum = E.P.Curve.Ceiling;
            slFloor.Minimum = E.P.Curve.Floor;
            slFloor.Maximum = E.P.Curve.Floor + (E.P.Curve.Ceiling - E.P.Curve.Floor) * 2 / 3;
            if (slSettingsPower != null) slSettingsPower.Maximum = E.MaxOffset;
        }


        bool dragging;
        void Drag() {
            dragging = true;
            try { DragMove(); } catch { } finally { dragging = false; }
        }
        void BuildRail() {
            var host = F<StackPanel>("NavHost");
            var bottom = F<StackPanel>("NavBottom");
            navBottom = bottom;
            nav[0] = new NavBtn(0, "Home", new[] { ICO_HOME_RING }, new[] { ICO_HOME_DOT });
            nav[1] = new NavBtn(1, "Fans", new[] { ICO_FANS }, new string[0]);
            nav[2] = new NavBtn(2, "Memory Cleaner", new[] { ICO_MEM_STROKE }, new[] { ICO_MEM_CHIPS });
            nav[3] = new NavBtn(3, "Overlay", new[] { ICO_OVERLAY }, new string[0]);
            nav[4] = new NavBtn(4, "Keyboard", new[] { ICO_KBD }, new string[0]);
            nav[5] = new NavBtn(5, "Settings", new string[0], new[] { GearPath(12, 12, 10, 7.6, 8, 3.4) });
            for (int i = 0; i < nav.Length; i++) { nav[i].HorizontalAlignment = HorizontalAlignment.Center; nav[i].Clicked += delegate(int idx) { Navigate((Page)idx, true); }; }
            host.Children.Add(nav[0]);
            host.Children.Add(nav[1]);
            host.Children.Add(nav[2]);
            host.Children.Add(nav[3]);
            host.Children.Add(nav[4]);

            navUpdate = new NavBtn(-1, "Update available", new[] { ICO_UPDATE }, new string[0]);
            navUpdate.HorizontalAlignment = HorizontalAlignment.Center;
            navUpdate.Accent = true;
            navUpdate.Visibility = Visibility.Collapsed;
            navUpdate.Clicked += delegate { ShowUpdateRow(); };
            bottom.Children.Add(navUpdate);
            bottom.Children.Add(nav[5]);
            bool showKbd = E.Light != null && E.S.KbdLighting;
            nav[4].Visibility = showKbd ? Visibility.Visible : Visibility.Collapsed;
            logoHost.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; Navigate(Page.Home, true); };
            rail.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) Drag(); };
            rail.SizeChanged += delegate { if (!morphing) PlaceRailPill(false); };
        }

        static string GearPath(double cx, double cy, double rOut, double rIn, int teeth, double hole) {
            var sb = new System.Text.StringBuilder();
            double step = 2 * Math.PI / teeth;
            for (int i = 0; i < teeth; i++) {
                double a0 = i * step;
                double[] angs = { a0 + 0.06 * step, a0 + 0.44 * step, a0 + 0.56 * step, a0 + 0.94 * step }; double[] rads = { rOut, rOut, rIn, rIn };
                for (int k = 0; k < 4; k++) {
                    double x = cx + rads[k] * Math.Cos(angs[k]), y = cy + rads[k] * Math.Sin(angs[k]);
                    sb.Append(i == 0 && k == 0 ? "M" : "L").Append(F2(x)).Append(' ').Append(F2(y)).Append(' ');
                }
            }
            sb.Append("Z ");
            string h = F2(hole), cys = F2(cy);
            sb.Append("M" + F2(cx - hole) + " " + cys + " A" + h + " " + h + " 0 1 0 " + F2(cx + hole) + " " + cys + " A" + h + " " + h + " 0 1 0 " + F2(cx - hole) + " " + cys + " Z");
            return "F0 " + sb.ToString();
        }
        static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        void PlaceRailPill(bool animate) {
            var b = nav[(int)cur];
            if (b.ActualWidth <= 0 || railCanvas.ActualWidth <= 0) return;
            Point p = b.TranslatePoint(new Point(0, 0), railCanvas);
            if (morphing && b.Parent == navBottom) p.Y += (mt[3] - mt[1]) - rail.ActualHeight;
            if (railPill.Opacity == 0 || !animate) { railPillT.BeginAnimation(TranslateTransform.XProperty, null); railPillT.BeginAnimation(TranslateTransform.YProperty, null); railPillT.X = p.X; railPillT.Y = p.Y; railPill.Opacity = 1; return; }
            Ui.Glide(railPillT, TranslateTransform.XProperty, p.X, 360, true);
            Ui.Glide(railPillT, TranslateTransform.YProperty, p.Y, 360, true);
        }

        void BuildHome() {
            modeSeg = new Seg(Engine.ModeNames, ModeSubs, null, Seg.Kind.Page);
            F<Border>("ModeHost").Child = modeSeg;
            modeSeg.Picked += ApplyModeAsync;
            fanLinks = new LinkSeg(new[] { "Auto", "Max", "Manual" }, 18, 13, 3, new[] { "This model's own curve", "Both fans at full speed", "Your own levels or curve, on the Fans page" });
            F<Border>("FanLinksHost").Child = fanLinks;
            fanLinks.Picked += delegate(int i) {
                if (i == 0) Bg(delegate { E.SetFan(FanMode.Auto, E.S.Fan1, E.S.Fan2, false); });
                else if (i == 1) Bg(delegate { E.SetFan(FanMode.Max, E.S.Fan1, E.S.Fan2, false); });
                else {
                    if (E.S.Fan == FanMode.Auto || E.S.Fan == FanMode.Max) { int f1 = E.S.Fan1, f2 = E.S.Fan2; E.S.Fan = FanMode.Manual; Bg(delegate { E.SetFan(FanMode.Manual, f1, f2, false); }); }
                    Navigate(Page.Fans, true);
                }
            };


            runCpuHead = new Run("CPU");
            runCpuWatts = new Run();
            runCpuTail = new Run();
            subCpu.Inlines.Clear();
            subCpu.Inlines.Add(runCpuHead);
            subCpu.Inlines.Add(runCpuWatts);
            subCpu.Inlines.Add(runCpuTail);
            if (E.Light == null) lightRow.Visibility = Visibility.Collapsed;
            else {
                var layout = BuildLayout();
                kbdMini = new KeyboardView { Interactive = false, Gap = 1.5, RowPitch = 9.5 }; kbdMini.SetLayout(layout); miniHost.Child = kbdMini;
                lightRow.MouseLeftButtonUp += delegate { Navigate(Page.Keyboard, true); };
                lightRow.MouseEnter += delegate { miniNeedsFrame = true; };
                if (!E.S.KbdLighting) lightRow.Visibility = Visibility.Collapsed;
            }
        }

        void BuildFans() {
            fanSeg = new Seg(Choice.Fan, new[] { "This model's own curve", "Both fans at full speed", "Your own curve, remembered per mode", "Fixed levels, held" }, null, Seg.Kind.Page);
            F<Border>("FanSegHost").Child = fanSeg;
            fanSeg.Picked += delegate(int i) {
                FanMode m = Choice.FanModes[i];
                int f1 = (int)slFan1.Value, f2 = (int)slFan2.Value;
                Bg(delegate { E.SetFan(m, f1, f2, false); });
                E.S.Fan = m;
                RefreshFans(true);
            };
            curveWhich = new LinkSeg(new[] { "CPU", "GPU" }, 12, 13, 3, null); curveWhichHost.Child = curveWhich;
            curveWhich.Picked += delegate(int i) { curveGpu = i == 1; RefreshFans(true); };
            curveView = new CurveView { Floor = E.P.Curve.Floor, Ceiling = E.P.Curve.Ceiling, Temps = Engine.CurveTemps, Levels = (int[])E.S.Cur.CurveLevels.Clone() };
            F<Border>("CurveHost").Child = curveView;
            curveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            curveDebounce.Tick += delegate { curveDebounce.Stop(); int[] lv = (int[])curveView.Levels.Clone(); bool gpu = curveGpu; Bg(delegate { E.SetCurve(lv, gpu); }); };
            curveView.Changed += delegate { curveDebounce.Stop(); curveDebounce.Start(); };
            btnFanAction.MouseLeftButtonUp += delegate {
                if (E.S.Fan == FanMode.Auto) { Bg(delegate { E.SeedCurveFromVendor(); }); E.S.Fan = FanMode.Custom; RefreshFans(true); return; }
                int[] v = E.VendorCurveAt(curveGpu);
                bool gpu = curveGpu;
                curveView.Levels = (int[])v.Clone();
                curveView.Repaint();
                Bg(delegate { E.SetCurve(v, gpu); });
            };
            OnSwitch(tgLink, delegate(bool on) { Bg(delegate { E.SetCurveLinked(on); }); });
            OnSwitch(tgEcoCool2, delegate(bool on) { Bg(delegate { E.SetEcoCool(on); }); });
            OnSwitch(tgMaxCool, delegate(bool on) { Bg(delegate { E.SetMaxBackWhenCool(on); }); });
            OnSwitch(tgManualLink, delegate(bool on) { Bg(delegate { E.SetManualLinked(on); }); });
            stopAfterSeg = new Seg(new[] { "15m", "30m", "60m", "Never" }, null, new object[] { 15, 30, 60, 0 }, Seg.Kind.Row); stopAfterSeg.SetMono(11.5);
            F<Border>("StopAfterHost").Child = stopAfterSeg;
            stopAfterSeg.Picked += delegate(int i) { int m = (int)stopAfterSeg.Tags[i]; Bg(delegate { E.SetMaxStopAfter(m); }); };
            floorDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            floorDebounce.Tick += delegate { floorDebounce.Stop(); int f = (int)slFloor.Value; Bg(delegate { E.SetCurveFloor(f); }); };
            slFloor.ValueChanged += delegate {
                int f = (int)slFloor.Value;
                txtFloor.Text = f <= E.P.Curve.Floor ? "off" : Pct(f);
                curveView.UserFloor = f <= E.P.Curve.Floor ? 0 : f;
                curveView.Repaint();
                if (!syncing) { floorDebounce.Stop(); floorDebounce.Start(); }
            };
            slRamp.ValueChanged += delegate { int r = (int)slRamp.Value; txtRamp.Text = r + " s"; if (!syncing) Bg(delegate { E.SetCurveRamp(r); }); };
            fanDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            fanDebounce.Tick += delegate { fanDebounce.Stop(); int f1 = (int)slFan1.Value, f2 = (int)slFan2.Value; Bg(delegate { E.SetFan(FanMode.Manual, f1, f2, false); }); };
            RoutedPropertyChangedEventHandler<double> fanSlid = delegate(object o, RoutedPropertyChangedEventArgs<double> ev) {
                bool was = syncing;
                if (!was && E.S.ManualLinked) {
                    syncing = true;
                    try { if (ReferenceEquals(o, slFan1)) slFan2.Value = slFan1.Value; else slFan1.Value = slFan2.Value; } finally { syncing = false; }
                }
                int f1 = (int)slFan1.Value, f2 = (int)slFan2.Value;
                txtFan1.Text = Pct(f1);
                txtFan2.Text = Pct(f2);
                manPct1.Text = Pct(f1).TrimEnd('%');
                manPct2.Text = Pct(f2).TrimEnd('%');
                manSub1.Text = E.Rpm(f1) + " · CPU fan";
                manSub2.Text = E.Rpm(f2) + " · GPU fan";
                if (!was && E.S.Fan == FanMode.Manual) { fanDebounce.Stop(); fanDebounce.Start(); }
            };
            slFan1.ValueChanged += fanSlid;
            slFan2.ValueChanged += fanSlid;
        }

        void BuildMemory() {
            var memIntervals = new[] { 0, 15, 30, 60, 120 };
            memIntervalSeg = new Seg(new[] { "Off", "15m", "30m", "1h", "2h" },
                new[] { "Disabled", "Every 15 minutes", "Every 30 minutes", "Every 1 hour", "Every 2 hours" }, null, Seg.Kind.Row);
            F<Border>("MemIntervalHost").Child = memIntervalSeg;
            int curIntIdx = Array.IndexOf(memIntervals, E.S.MemCleanIntervalMin);
            if (curIntIdx < 0) curIntIdx = 0;
            memIntervalSeg.Select(curIntIdx, false);
            memIntervalSeg.Picked += delegate(int i) {
                if (i >= 0 && i < memIntervals.Length) {
                    E.S.MemCleanIntervalMin = memIntervals[i];
                    E.S.Save();
                }
            };

            var memThresholds = new[] { 0, 5, 10, 15, 20 };
            memThresholdSeg = new Seg(new[] { "Off", "5%", "10%", "15%", "20%" },
                new[] { "Disabled", "When free RAM < 5%", "When free RAM < 10%", "When free RAM < 15%", "When free RAM < 20%" }, null, Seg.Kind.Row);
            F<Border>("MemThresholdHost").Child = memThresholdSeg;
            int curThreshIdx = Array.IndexOf(memThresholds, E.S.MemCleanThresholdPct);
            if (curThreshIdx < 0) curThreshIdx = 0;
            memThresholdSeg.Select(curThreshIdx, false);
            memThresholdSeg.Picked += delegate(int i) {
                if (i >= 0 && i < memThresholds.Length) {
                    E.S.MemCleanThresholdPct = memThresholds[i];
                    E.S.Save();
                }
            };

            tgMemWorkingSet.IsChecked = E.S.MemCleanWorkingSet;
            tgMemSystemCache.IsChecked = E.S.MemCleanSystemCache;
            tgMemStandby.IsChecked = E.S.MemCleanStandby;
            tgMemModified.IsChecked = E.S.MemCleanModified;
            tgMemCombined.IsChecked = E.S.MemCleanCombined;
            tgMemRegistry.IsChecked = E.S.MemCleanRegistry;
            tgMemNotify.IsChecked = E.S.MemCleanNotify;

            OnSwitch(tgMemWorkingSet, delegate(bool on) { E.S.MemCleanWorkingSet = on; E.S.Save(); });
            OnSwitch(tgMemSystemCache, delegate(bool on) { E.S.MemCleanSystemCache = on; E.S.Save(); });
            OnSwitch(tgMemStandby, delegate(bool on) { E.S.MemCleanStandby = on; E.S.Save(); });
            OnSwitch(tgMemModified, delegate(bool on) { E.S.MemCleanModified = on; E.S.Save(); });
            OnSwitch(tgMemCombined, delegate(bool on) { E.S.MemCleanCombined = on; E.S.Save(); });
            OnSwitch(tgMemRegistry, delegate(bool on) { E.S.MemCleanRegistry = on; E.S.Save(); });
            OnSwitch(tgMemNotify, delegate(bool on) { E.S.MemCleanNotify = on; E.S.Save(); });

            btnOptimizeMem.MouseLeftButtonUp += delegate {
                PerformManualOptimize();
            };

            scrollMemory.PreviewMouseWheel += delegate(object o, MouseWheelEventArgs e) {
                e.Handled = true;
                scrollMemoryTo = Math.Max(0, Math.Min(scrollMemory.ScrollableHeight, scrollMemoryTo - e.Delta / 120.0 * 54));
                if (!scrollingMemory) { scrollingMemory = true; CompositionTarget.Rendering += ScrollMemoryTick; }
            };
            scrollMemory.ScrollChanged += delegate { if (!scrollingMemory) scrollMemoryTo = scrollMemory.VerticalOffset; };

            UpdateMemoryLive();
        }

        void ScrollMemoryTick(object o, EventArgs e) {
            double at = scrollMemory.VerticalOffset, d = scrollMemoryTo - at;
            if (Math.Abs(d) < 0.6) { scrollMemory.ScrollToVerticalOffset(scrollMemoryTo); scrollingMemory = false; CompositionTarget.Rendering -= ScrollMemoryTick; return; }
            scrollMemory.ScrollToVerticalOffset(at + d * 0.28);
        }

        void UpdateMemoryLive() {
            var stats = MemoryCleaner.GetStats();
            if (txtMemTotal != null) txtMemTotal.Text = string.Format("{0:F1} GB Total", stats.TotalGb);
            if (txtMemUsed != null) txtMemUsed.Text = stats.UsedGb.ToString("0.0", CultureInfo.InvariantCulture);
            if (txtMemUsedPct != null) txtMemUsedPct.Text = string.Format("Used RAM ({0}%)", stats.UsedPercent);
            if (txtMemFree != null) txtMemFree.Text = stats.FreeGb.ToString("0.0", CultureInfo.InvariantCulture);
            if (txtMemFreePct != null) txtMemFreePct.Text = string.Format("Free RAM ({0}%)", stats.FreePercent);

            var host = F<Grid>("MemBarHost");
            if (host != null && host.ActualWidth > 0 && memBarUsed != null) {
                double pct = Math.Max(0.02, Math.Min(0.98, stats.UsedPercent / 100.0));
                memBarUsed.Width = host.ActualWidth * pct;
            }
        }

        void PerformManualOptimize() {
            if (optimizingMem) return;
            optimizingMem = true;
            btnOptimizeMem.Opacity = 0.6;
            txtOptimizeBtn.Text = "Optimizing…";
            txtMemStatus.Text = "Cleaning selected memory areas…";
            Slow(delegate {
                int procCount;
                long freed = E.CleanMemory(out procCount);
                double freedMb = freed / (1024.0 * 1024.0);
                Dispatcher.BeginInvoke((Action)delegate {
                    optimizingMem = false;
                    btnOptimizeMem.Opacity = 1.0;
                    txtOptimizeBtn.Text = "Optimize Memory";
                    txtMemStatus.Text = string.Format("Freed {0:F0} MB across {1} processes", freedMb, procCount);
                    UpdateMemoryLive();
                    if (E.S.MemCleanNotify) ShowToast(string.Format("Freed {0:F0} MB of RAM", freedMb), false);
                });
            });
        }

        void CheckAutoMemoryClean() {
            var now = DateTime.UtcNow;
            if (E.S.MemCleanIntervalMin > 0) {
                if (lastMemAutoClean == DateTime.MinValue) lastMemAutoClean = now;
                else if ((now - lastMemAutoClean).TotalMinutes >= E.S.MemCleanIntervalMin) {
                    lastMemAutoClean = now;
                    RunBackgroundMemoryClean("Interval auto-clean");
                }
            }
            if (E.S.MemCleanThresholdPct > 0) {
                if (lastMemThresholdClean == DateTime.MinValue || (now - lastMemThresholdClean).TotalSeconds >= 60) {
                    var stats = MemoryCleaner.GetStats();
                    if (stats.FreePercent < E.S.MemCleanThresholdPct) {
                        lastMemThresholdClean = now;
                        RunBackgroundMemoryClean(string.Format("Low memory trigger ({0}% free)", stats.FreePercent));
                    }
                }
            }
        }

        void RunBackgroundMemoryClean(string reason) {
            Slow(delegate {
                int procCount;
                long freed = E.CleanMemory(out procCount);
                double freedMb = freed / (1024.0 * 1024.0);
                Dispatcher.BeginInvoke((Action)delegate {
                    if (cur == Page.Memory) {
                        txtMemStatus.Text = string.Format("{0}: freed {1:F0} MB", reason, freedMb);
                        UpdateMemoryLive();
                    }
                    if (E.S.MemCleanNotify && freedMb > 10) {
                        ShowToast(string.Format("Memory cleaner freed {0:F0} MB", freedMb), false);
                    }
                });
            });
        }

        void BuildOverlay() {
            overlayPerfSeg = new Seg(new[] { "Eco", "Quiet", "Balanced", "Performance" }, null, null, Seg.Kind.Row);
            overlayPerfSeg.SetMono(11.5);
            F<Border>("OverlayPerfSegHost").Child = overlayPerfSeg;
            overlayPerfSeg.Select(E.S.OverlayPerfMode, false);
            overlayPerfSeg.Picked += delegate(int i) {
                Bg(delegate { E.SetOverlayPerf(i); });
            };

            overlayOrientSeg = new Seg(new[] { "Horizontal Bar", "2-Col Grid" }, null, null, Seg.Kind.Row);
            F<Border>("OverlayOrientHost").Child = overlayOrientSeg;
            overlayOrientSeg.Select(E.S.OverlayHorizontal ? 0 : 1, false);
            overlayOrientSeg.Picked += delegate(int i) {
                E.S.OverlayHorizontal = (i == 0);
                E.S.Save();
                if (overlayWin != null) overlayWin.ApplyLayout();
            };

            tgOverlayEnable.IsChecked = E.S.Overlay;
            OnSwitch(tgOverlayEnable, delegate(bool on) {
                E.SetOverlay(on);
                SyncOverlayWindow(on);
            });

            tgOverlayPin.IsChecked = E.S.OverlayPinned;
            OnSwitch(tgOverlayPin, delegate(bool on) {
                E.S.OverlayPinned = on;
                if (on) {
                    E.S.OverlayPerfPinned = false;
                    if (overlayPerfWin != null) {
                        overlayPerfWin.UpdatePinState();
                        overlayPerfWin.Hide();
                    }
                }
                E.S.Save();
                if (overlayWin != null) {
                    overlayWin.UpdatePinState();
                    overlayWin.Show();
                }
            });

            slOverlayOpacity.Value = E.S.OverlayOpacity;
            txtOverlayOpacity.Text = E.S.OverlayOpacity + "%";
            slOverlayOpacity.ValueChanged += delegate {
                int op = (int)slOverlayOpacity.Value;
                txtOverlayOpacity.Text = op + "%";
                E.S.OverlayOpacity = op;
                E.S.Save();
                if (overlayWin != null) overlayWin.ApplyLayout();
                if (overlayPerfWin != null) overlayPerfWin.UpdateState();
            };

            tgOverlayCpu.IsChecked = E.S.OverlayShowCpu;
            tgOverlayGpu.IsChecked = E.S.OverlayShowGpu;
            tgOverlayRam.IsChecked = E.S.OverlayShowRam;
            tgOverlayFps.IsChecked = E.S.OverlayShowFps;
            tgOverlayUpload.IsChecked = E.S.OverlayShowUpload;
            tgOverlayDownload.IsChecked = E.S.OverlayShowDownload;
            tgOverlayPerf.IsChecked = E.S.OverlayShowPerf;

            OnSwitch(tgOverlayCpu, delegate(bool on) { E.S.OverlayShowCpu = on; E.S.Save(); if (overlayWin != null) overlayWin.ApplyLayout(); });
            OnSwitch(tgOverlayGpu, delegate(bool on) { E.S.OverlayShowGpu = on; E.S.Save(); if (overlayWin != null) overlayWin.ApplyLayout(); });
            OnSwitch(tgOverlayRam, delegate(bool on) { E.S.OverlayShowRam = on; E.S.Save(); if (overlayWin != null) overlayWin.ApplyLayout(); });
            OnSwitch(tgOverlayFps, delegate(bool on) { E.S.OverlayShowFps = on; E.S.Save(); if (overlayWin != null) overlayWin.ApplyLayout(); });
            OnSwitch(tgOverlayUpload, delegate(bool on) { E.S.OverlayShowUpload = on; E.S.Save(); if (overlayWin != null) overlayWin.ApplyLayout(); });
            OnSwitch(tgOverlayDownload, delegate(bool on) { E.S.OverlayShowDownload = on; E.S.Save(); if (overlayWin != null) overlayWin.ApplyLayout(); });
            OnSwitch(tgOverlayPerf, delegate(bool on) {
                E.S.OverlayShowPerf = on;
                E.S.Save();
                if (overlayWin != null) overlayWin.ApplyLayout();
                SyncOverlayPerfWindow(on);
            });

            if (tgOverlayDock != null) {
                tgOverlayDock.IsChecked = E.S.OverlayDock;
                OnSwitch(tgOverlayDock, delegate(bool on) {
                    E.S.OverlayDock = on;
                    E.S.Save();
                    SyncOverlayDockWindow(on);
                });
            }

            if (btnOverlayHotkey != null) {
                Action updateHotkeyUi = delegate {
                    string text = string.IsNullOrEmpty(E.S.OverlayHotkey) ? "Shift+F2" : E.S.OverlayHotkey;
                    txtOverlayHotkey.Text = text;
                    txtOverlayHotkey.Foreground = Ui.Brush("#EDEAE8");
                    btnOverlayHotkey.BorderBrush = Ui.Line;
                    btnOverlayHotkey.Background = Ui.Sunken;
                    if (overlayDockWin != null) overlayDockWin.UpdateHotkeyDisplay();
                };
                updateHotkeyUi();

                btnOverlayHotkey.MouseLeftButtonUp += delegate {
                    isRecordingOverlayHotkey = !isRecordingOverlayHotkey;
                    if (isRecordingOverlayHotkey) {
                        txtOverlayHotkey.Text = "Press keys...";
                        txtOverlayHotkey.Foreground = Ui.Brush(Ui.Accent.Color);
                        btnOverlayHotkey.BorderBrush = Ui.Brush(Ui.Accent.Color);
                        btnOverlayHotkey.Background = Ui.Brush("#201C19");
                    } else {
                        updateHotkeyUi();
                    }
                };

                PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
                    if (!isRecordingOverlayHotkey) return;
                    Key k = e.Key == Key.System ? e.SystemKey : e.Key;
                    if (k == Key.Escape) {
                        isRecordingOverlayHotkey = false;
                        updateHotkeyUi();
                        e.Handled = true;
                        return;
                    }
                    if (k == Key.Delete || k == Key.Back) {
                        isRecordingOverlayHotkey = false;
                        E.S.OverlayHotkey = "Disabled";
                        E.S.Save();
                        UnregisterOverlayHotkey();
                        updateHotkeyUi();
                        ShowToast("Overlay keybind disabled", false);
                        e.Handled = true;
                        return;
                    }
                    switch (k) {
                        case Key.LeftCtrl: case Key.RightCtrl:
                        case Key.LeftAlt: case Key.RightAlt:
                        case Key.LeftShift: case Key.RightShift:
                        case Key.LWin: case Key.RWin:
                            return;
                    }
                    Hotkey hk;
                    if (Hotkey.FromKey(k, Keyboard.Modifiers, out hk) && hk.Valid) {
                        isRecordingOverlayHotkey = false;
                        string str = hk.ToString();
                        E.S.OverlayHotkey = str;
                        E.S.Save();
                        UnregisterOverlayHotkey();
                        RegisterOverlayHotkey();
                        updateHotkeyUi();
                        ShowToast("Overlay keybind set to " + str, false);
                        e.Handled = true;
                    }
                };

                MouseDown += delegate(object sender, MouseButtonEventArgs e) {
                    if (isRecordingOverlayHotkey && !btnOverlayHotkey.IsMouseOver) {
                        isRecordingOverlayHotkey = false;
                        updateHotkeyUi();
                    }
                };
            }

            btnOpenOverlay.MouseLeftButtonUp += delegate {
                E.SetOverlay(true);
                SyncOverlayWindow(true);
                if (overlayDockWin != null && E.S.OverlayDock) {
                    overlayDockWin.Show();
                    overlayDockWin.Activate();
                }
                if (overlayWin != null && !E.S.OverlayPerfPinned) {
                    overlayWin.ApplyLayout();
                    overlayWin.Show();
                    overlayWin.Activate();
                }
                if (overlayPerfWin != null && !E.S.OverlayPinned && E.S.OverlayShowPerf) {
                    overlayPerfWin.UpdateState();
                    overlayPerfWin.Show();
                    overlayPerfWin.Activate();
                }
                StartDismissTimer();
                ShowToast("Overlay active (drag anywhere to move)", false);
            };

            btnResetOverlayPos.MouseLeftButtonUp += delegate {
                if (overlayWin != null) overlayWin.ResetPosition();
                if (overlayPerfWin != null) overlayPerfWin.ResetPosition();
                if (overlayDockWin != null) overlayDockWin.ResetPosition();
                ShowToast("Overlay positions reset", false);
            };

            scrollOverlay.PreviewMouseWheel += delegate(object o, MouseWheelEventArgs e) {
                e.Handled = true;
                scrollOverlay.ScrollToVerticalOffset(Math.Max(0, Math.Min(scrollOverlay.ScrollableHeight, scrollOverlay.VerticalOffset - e.Delta / 120.0 * 54)));
            };

            if (cardOverlayEnable != null) {
                cardOverlayEnable.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) {
                    if (e.OriginalSource == tgOverlayEnable || VisualTreeHelper.GetParent(e.OriginalSource as DependencyObject) == tgOverlayEnable) return;
                    tgOverlayEnable.IsChecked = !tgOverlayEnable.IsChecked;
                };
            }

            if (E.S.Overlay) SyncOverlayWindow(true);
        }

        void UpdateDockState() {
            if (overlayDockWin != null) {
                overlayDockWin.SetState(
                    overlayWin != null && overlayWin.IsVisible,
                    overlayPerfWin != null && overlayPerfWin.IsVisible,
                    E.S.Fan == FanMode.Max,
                    IsVisible && WindowState != WindowState.Minimized
                );
            }
        }

        void ShowWholeUi() {
            IntPtr fore = GetForegroundWindow();
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            bool isFore = (fore != IntPtr.Zero && fore == myHwnd);

            if (IsVisible && WindowState == WindowState.Normal && isFore && cur == Page.Home) {
                HideToTray();
            } else {
                Navigate(Page.Home, false);
                ShowPanel();
            }
            UpdateDockState();
        }

        void ShowOverlaySettings() {
            IntPtr fore = GetForegroundWindow();
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            bool isFore = (fore != IntPtr.Zero && fore == myHwnd);

            if (IsVisible && WindowState == WindowState.Normal && isFore && cur == Page.Overlay) {
                HideToTray();
            } else {
                Navigate(Page.Overlay, false);
                ShowPanel();
            }
            UpdateDockState();
        }

        void SyncOverlayWindow(bool show) {
            try {
                if (show) {
                    if (overlayWin == null) {
                        overlayWin = new OverlayWindow(E);
                        overlayWin.IsVisibleChanged += delegate { UpdateDockState(); };
                        overlayWin.OnPerfVisibilityChanged = delegate(bool on) {
                            if (tgOverlayPerf != null) tgOverlayPerf.IsChecked = on;
                            SyncOverlayPerfWindow(on);
                            UpdateDockState();
                        };
                        overlayWin.OnPinnedChanged = delegate(bool pinned) {
                            if (tgOverlayPin != null) tgOverlayPin.IsChecked = pinned;
                            if (pinned) {
                                E.S.OverlayPerfPinned = false;
                                if (overlayPerfWin != null) {
                                    overlayPerfWin.UpdatePinState();
                                    overlayPerfWin.Hide();
                                }
                            }
                            UpdateDockState();
                        };
                    }
                    if (overlayPerfWin == null) {
                        overlayPerfWin = new OverlayPerfWindow(E);
                        overlayPerfWin.IsVisibleChanged += delegate { UpdateDockState(); };
                        overlayPerfWin.OnPinnedChanged = delegate(bool pinned) {
                            if (pinned) {
                                E.S.OverlayPinned = false;
                                if (tgOverlayPin != null) tgOverlayPin.IsChecked = false;
                                if (overlayWin != null) {
                                    overlayWin.UpdatePinState();
                                    overlayWin.Hide();
                                }
                            }
                            UpdateDockState();
                        };
                    }

                    if (E.S.OverlayPerfPinned && !E.S.OverlayPinned) {
                        overlayWin.Hide();
                        overlayPerfWin.UpdateState();
                        overlayPerfWin.Show();
                    } else if (E.S.OverlayPinned && !E.S.OverlayPerfPinned) {
                        overlayWin.ApplyLayout();
                        overlayWin.Show();
                        overlayPerfWin.Hide();
                    } else {
                        overlayWin.ApplyLayout();
                        overlayWin.Show();
                        if (E.S.OverlayShowPerf) {
                            overlayPerfWin.UpdateState();
                            overlayPerfWin.Show();
                        } else {
                            overlayPerfWin.Hide();
                        }
                    }

                    SyncOverlayDockWindow(E.S.OverlayDock);
                } else {
                    if (overlayWin != null) overlayWin.Hide();
                    if (overlayPerfWin != null) overlayPerfWin.Hide();
                    if (overlayDockWin != null) overlayDockWin.Hide();
                }
                UpdateDockState();
            } catch (Exception ex) {
                Log.Write("overlay sync error: " + ex);
            }
        }

        void SyncOverlayPerfWindow(bool show) {
            try {
                if (show && E.S.Overlay && E.S.OverlayShowPerf) {
                    if (overlayPerfWin == null) {
                        overlayPerfWin = new OverlayPerfWindow(E);
                        overlayPerfWin.IsVisibleChanged += delegate { UpdateDockState(); };
                        overlayPerfWin.OnPinnedChanged = delegate(bool pinned) {
                            if (pinned) {
                                E.S.OverlayPinned = false;
                                if (tgOverlayPin != null) tgOverlayPin.IsChecked = false;
                                if (overlayWin != null) {
                                    overlayWin.UpdatePinState();
                                    overlayWin.Hide();
                                }
                            }
                            UpdateDockState();
                        };
                    }
                    if (!E.S.OverlayPinned) {
                        overlayPerfWin.UpdateState();
                        overlayPerfWin.Show();
                    }
                } else {
                    if (overlayPerfWin != null) overlayPerfWin.Hide();
                }
                UpdateDockState();
            } catch (Exception ex) {
                Log.Write("overlay perf sync error: " + ex);
            }
        }

        void SyncOverlayDockWindow(bool show) {
            try {
                if (show && E.S.Overlay) {
                    if (overlayDockWin == null) {
                        overlayDockWin = new OverlayDockWindow(E);
                        overlayDockWin.OnToggleVitals = delegate {
                            if (overlayWin == null) SyncOverlayWindow(true);
                            if (overlayWin != null) {
                                if (overlayWin.IsVisible) {
                                    overlayWin.Hide();
                                } else {
                                    overlayWin.ApplyLayout();
                                    overlayWin.Show();
                                    overlayWin.Activate();
                                }
                                UpdateDockState();
                            }
                        };
                        overlayDockWin.OnTogglePerf = delegate {
                            if (overlayPerfWin == null) SyncOverlayWindow(true);
                            if (overlayPerfWin != null) {
                                if (overlayPerfWin.IsVisible) {
                                    overlayPerfWin.Hide();
                                } else {
                                    overlayPerfWin.UpdateState();
                                    overlayPerfWin.Show();
                                    overlayPerfWin.Activate();
                                }
                                UpdateDockState();
                            }
                        };
                        overlayDockWin.OnToggleBars = delegate {
                            E.S.OverlayHorizontal = !E.S.OverlayHorizontal;
                            E.S.Save();
                            if (overlayOrientSeg != null) overlayOrientSeg.Select(E.S.OverlayHorizontal ? 0 : 1, false);
                            if (overlayWin != null) {
                                overlayWin.ApplyLayout();
                                if (!overlayWin.IsVisible) overlayWin.Show();
                            }
                            UpdateDockState();
                        };
                        overlayDockWin.OnToggleFan = delegate {
                            ToggleMaxWithFlash();
                            UpdateDockState();
                        };
                        overlayDockWin.OnHotkeyClick = delegate {
                            HandleOverlayHotkey();
                        };
                        overlayDockWin.OnOpenWholeUi = delegate {
                            ShowWholeUi();
                        };
                        overlayDockWin.OnOpenSettings = delegate {
                            ShowOverlaySettings();
                        };
                    }

                    UpdateDockState();
                } else {
                    if (overlayDockWin != null) overlayDockWin.Hide();
                }
            } catch (Exception ex) {
                Log.Write("overlay dock sync error: " + ex);
            }
        }

        void RegisterOverlayHotkey() {
            if (overlayHotkeyRegistered) return;
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;
            if (string.IsNullOrEmpty(E.S.OverlayHotkey) || E.S.OverlayHotkey == "Disabled") return;
            Hotkey hk;
            if (Hotkey.TryParse(E.S.OverlayHotkey, out hk) && !hk.IsEmpty) {
                if (RegisterHotKey(h, HOTKEY_OVERLAY, hk.Mods | MOD_NOREPEAT, hk.Vk)) {
                    overlayHotkeyRegistered = true;
                } else {
                    Log.Write("overlay hotkey " + E.S.OverlayHotkey + " not available: another program has it");
                }
            }
        }

        void UnregisterOverlayHotkey() {
            if (!overlayHotkeyRegistered) return;
            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero) UnregisterHotKey(h, HOTKEY_OVERLAY);
            overlayHotkeyRegistered = false;
        }

        void StartDismissTimer() {
            if (tempDismissTimer != null) tempDismissTimer.Stop();
            tempDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            DateTime start = DateTime.UtcNow;

            tempDismissTimer.Tick += delegate {
                bool hasUnpinnedVisible = (overlayDockWin != null && overlayDockWin.IsVisible) ||
                                          (overlayWin != null && overlayWin.IsVisible && !E.S.OverlayPinned) ||
                                          (overlayPerfWin != null && overlayPerfWin.IsVisible && !E.S.OverlayPerfPinned);

                if (!hasUnpinnedVisible) {
                    tempDismissTimer.Stop();
                    return;
                }

                if ((DateTime.UtcNow - start).TotalMilliseconds < 350) return;

                bool lDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
                bool rDown = (GetAsyncKeyState(0x02) & 0x8000) != 0;
                if (lDown || rDown) {
                    POINT pt;
                    GetCursorPos(out pt);
                    bool insideAny = false;

                    if (overlayDockWin != null && overlayDockWin.IsVisible) {
                        IntPtr hd = new WindowInteropHelper(overlayDockWin).Handle;
                        RECT rd;
                        if (GetWindowRect(hd, out rd) && pt.X >= rd.Left && pt.X <= rd.Right && pt.Y >= rd.Top && pt.Y <= rd.Bottom)
                            insideAny = true;
                    }
                    if (overlayWin != null && overlayWin.IsVisible) {
                        IntPtr h1 = new WindowInteropHelper(overlayWin).Handle;
                        RECT r1;
                        if (GetWindowRect(h1, out r1) && pt.X >= r1.Left && pt.X <= r1.Right && pt.Y >= r1.Top && pt.Y <= r1.Bottom)
                            insideAny = true;
                    }
                    if (overlayPerfWin != null && overlayPerfWin.IsVisible) {
                        IntPtr h2 = new WindowInteropHelper(overlayPerfWin).Handle;
                        RECT r2;
                        if (GetWindowRect(h2, out r2) && pt.X >= r2.Left && pt.X <= r2.Right && pt.Y >= r2.Top && pt.Y <= r2.Bottom)
                            insideAny = true;
                    }

                    if (!insideAny) {
                        tempDismissTimer.Stop();
                        if (overlayDockWin != null && overlayDockWin.IsVisible) overlayDockWin.Hide();
                        if (overlayWin != null && overlayWin.IsVisible && !E.S.OverlayPinned) overlayWin.Hide();
                        if (overlayPerfWin != null && overlayPerfWin.IsVisible && !E.S.OverlayPerfPinned) overlayPerfWin.Hide();
                        UpdateDockState();
                    }
                }
            };
            tempDismissTimer.Start();
        }

        void HandleOverlayHotkey() {
            if (string.IsNullOrEmpty(E.S.OverlayHotkey) || E.S.OverlayHotkey == "Disabled") return;

            if (overlayWin == null || overlayPerfWin == null) SyncOverlayWindow(true);
            if (overlayDockWin == null && E.S.Overlay && E.S.OverlayDock) SyncOverlayDockWindow(true);

            bool dockVisible = overlayDockWin != null && overlayDockWin.IsVisible;
            bool unpinnedVisible = (overlayWin != null && overlayWin.IsVisible && !E.S.OverlayPinned) ||
                                  (overlayPerfWin != null && overlayPerfWin.IsVisible && !E.S.OverlayPerfPinned);

            if (dockVisible || unpinnedVisible) {
                if (overlayDockWin != null) overlayDockWin.Hide();
                if (overlayWin != null && !E.S.OverlayPinned) overlayWin.Hide();
                if (overlayPerfWin != null && !E.S.OverlayPerfPinned) overlayPerfWin.Hide();
                if (tempDismissTimer != null) tempDismissTimer.Stop();
            } else {
                if (overlayDockWin != null && E.S.OverlayDock) {
                    overlayDockWin.Show();
                    overlayDockWin.Activate();
                }

                if (E.S.OverlayPinned && !E.S.OverlayPerfPinned) {

                    if (overlayPerfWin != null) {
                        overlayPerfWin.UpdateState();
                        overlayPerfWin.Show();
                        overlayPerfWin.Activate();
                    }
                } else if (E.S.OverlayPerfPinned && !E.S.OverlayPinned) {

                    if (overlayWin != null) {
                        overlayWin.ApplyLayout();
                        overlayWin.Show();
                        overlayWin.Activate();
                    }
                } else {

                    if (overlayWin != null) {
                        overlayWin.ApplyLayout();
                        overlayWin.Show();
                    }
                    if (overlayPerfWin != null && E.S.OverlayShowPerf) {
                        overlayPerfWin.UpdateState();
                        overlayPerfWin.Show();
                    }
                }
                StartDismissTimer();
            }
            UpdateDockState();
        }

        void BuildSettings() {
            keySeg = new Seg(Choice.Key, new[] { "Next mode", "Open this window", "Toggle max fan", "Start a command of your choice", null }, null, Seg.Kind.Compact);
            F<Border>("KeySegHost").Child = keySeg;
            keySeg.Picked += delegate(int i) {
                KeyAction a = Choice.KeyActions[i];
                E.SetKey(a);
                keyCmdRow.Visibility = a == KeyAction.Run ? Visibility.Visible : Visibility.Collapsed;
            };
            gpuSeg = new Seg(new[] { "Base", "Boost", "Max", "Auto" },
                new[] { "The GPU's standard power limit",
                        "Lets the GPU borrow power from the CPU when it needs it",
                        "A raised power limit as well as the borrowing",
                        "Base in Eco, Boost in Balanced, Max in Performance" }, null, Seg.Kind.Row);
            F<Border>("GpuSegHost").Child = gpuSeg;


            var pollMs = new[] { 500, 1000, 2000 };
            pollSeg = new Seg(new[] { "0.5s", "1s", "2s" },
                new[] { "Updates twice a second", "Updates every second", "The default" }, null, Seg.Kind.Row);
            F<Border>("PollSegHost").Child = pollSeg;
            pollSeg.Picked += delegate(int i) {
                if (i < 0 || i >= pollMs.Length) return;
                E.S.PollMs = pollMs[i];
                E.S.Save();
                PollRate();
            };
            gpuSeg.Picked += delegate(int i) {
                if (i == 3) Bg(delegate { E.SetGpu(E.S.Gpu, true, false); });
                else { GpuLevel g = (GpuLevel)i; Bg(delegate { E.SetGpu(g, false, false); }); }
            };
            if (!E.P.HasGpuPower) gpuRow.Visibility = Visibility.Collapsed;
            txtKeyCmd.LostFocus += delegate { E.SetKeyCommand(txtKeyCmd.Text); };
            txtKeyCmd.TextChanged += delegate { txtKeyCmdHint.Visibility = txtKeyCmd.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            txtKeyCmd.KeyDown += delegate(object o, KeyEventArgs ke) { if (ke.Key == Key.Enter) { E.SetKeyCommand(txtKeyCmd.Text); ShowToast("OMEN key runs: " + (E.S.KeyCommand.Length > 0 ? E.S.KeyCommand : "(nothing)"), false); } };
            BuildRefreshRates();
            BuildGraphicsModes();
            learnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            learnTimer.Tick += delegate { learnTimer.Stop(); E.Learning = false; UpdateKeyStatus(); };
            btnLearn.MouseLeftButtonUp += delegate { E.Learning = true; keyDot.Fill = Ui.Brush(Ui.Warn); txtKeyInfo.Text = "press the OMEN key now… (10 s)"; learnTimer.Stop(); learnTimer.Start(); };

            tgKbdLighting.IsChecked = E.S.KbdLighting;
            OnSwitch(tgKbdLighting, delegate(bool on) { Bg(delegate { E.SetKbdLighting(on); }); });

            OnSwitch(tgHotkeys, delegate(bool on) { StopListening(); Bg(delegate { E.SetHotkeys(on); }); if (on) RegisterHotkeys(); else UnregisterHotkeys(); });
            btnHotkeys.Click += delegate { ToggleHotkeyPanel(); };
            btnHotkeys.Content = PencilGlyph(false);
            PreviewKeyUp += delegate { ShowHeldModifiers(); };
            BuildGuardLine();


            txtGuardSub.Visibility = Visibility.Collapsed;
            guardLine.Visibility = Visibility.Visible;
            btnGuard.Content = PencilGlyph(false);
            btnGuard.ToolTip = "Change the rule";
            btnGuard.Click += delegate {
                guardOpen = !guardOpen;
                btnGuard.Content = PencilGlyph(guardOpen);
                btnGuard.ToolTip = guardOpen ? "Done" : "Change the rule";
                foreach (ValueLink v in new[] { chipCpu, chipChassis, chipFans, chipHold }) v.Editable = guardOpen;
            };
            guardDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            guardDebounce.Tick += delegate {
                guardDebounce.Stop();
                int cpu = cpuLo + chipCpu.Index, ch = chassisLo + chipChassis.Index, lvl = guardLevels[chipFans.Index], hold = holdChoices[chipHold.Index];
                Bg(delegate { E.SetGuardLimits(cpu, ch, lvl, hold); });
            };
            PreviewKeyDown += OnHotkeyCapture;
            Deactivated += delegate { StopListening(); };
            OnSwitch(tgEcoBattery, delegate(bool on) { Bg(delegate { E.SetEcoOnBattery(on); }); });
            OnSwitch(tgSyncPower, delegate(bool on) { Bg(delegate { E.SetSyncWinPower(on); }); });
            OnSwitch(tgLowHzBattery, delegate(bool on) { Bg(delegate { E.SetLowHzOnBattery(on); }); });
            OnSwitch(tgTrayTemp, delegate(bool on) {
                Bg(delegate { E.SetTrayTemp(on); });
                if (!on) { try { tray.Icon = icons[E.ModeIndex]; } catch { } }
                PollRate();
            });
            OnSwitch(tgSuppress, delegate(bool on) { Bg(delegate { E.SetOghSuppression(on); }); });
            OnSwitch(tgAutostart, delegate(bool on) { Slow(delegate { SetAutostart(on); }); });
            OnSwitch(tgGuard, delegate(bool on) {
                if (!on && !ConfirmGuardOff()) { Synced(delegate { tgGuard.IsChecked = true; }); return; }
                Bg(delegate { E.SetGuard(on); });
            });
            OnSwitch(tgUpdateAuto, delegate(bool on) { Bg(delegate { E.SetUpdateOnLaunch(on); }); });
            OnSwitch(tgDriver, delegate(bool on) { Bg(delegate { E.SetDriverUse(on); }); });

            btnDriver.MouseLeftButtonUp += delegate { DriverAction(); };
            btnDriverNudge.MouseLeftButtonUp += delegate { InstallDriver(); };
            driverClose.MouseLeftButtonUp += delegate { E.DismissDriverNudge(); driverBanner.Visibility = Visibility.Collapsed; Remeasure(cur); };

            btnUpdate.MouseLeftButtonUp += delegate {
                if (E.Staged != null || E.UpdateAvailable) { OpenReleases(); return; }
                txtUpdate.Text = "checking…"; Slow(delegate { E.CheckForUpdate(true); });
            };
            updateText.MouseLeftButtonUp += delegate { if (E.Staged != null) DoUpdate(); };
            btnDiag.MouseLeftButtonUp += delegate {
                if (txtDiag.Visibility == Visibility.Visible) { txtDiag.Visibility = Visibility.Collapsed; return; }
                txtDiag.Text = "running…";
                txtDiag.Visibility = Visibility.Visible;
                Slow(delegate { string d = E.Diagnostics(); Log.Write(d); Dispatcher.BeginInvoke((Action)delegate { txtDiag.Text = d.TrimEnd(); }); });
            };

            btnSupport.MouseLeftButtonUp += delegate {
                btnSupport.Text = "collecting…";
                Slow(delegate {
                    string r;
                    try { r = Support.Report(E); } catch (Exception ex) { r = "support report failed: " + ex.Message; }
                    string path = "";
                    try {
                        path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Log.Path), "support-info.txt");
                        System.IO.File.WriteAllText(path, r);
                    } catch { path = ""; }
                    Dispatcher.BeginInvoke((Action)delegate {
                        btnSupport.Text = "Report Issue";
                        bool copied = false;
                        try { Clipboard.SetText(r); copied = true; } catch { }
                        txtDiag.Text = r.TrimEnd();
                        txtDiag.Visibility = Visibility.Visible;
                        Remeasure(cur);
                        ShowToast(copied ? "Copied. Paste it into the issue, or drag support-info.txt in"
                                         : "Saved as support-info.txt, drag it into the issue", !copied);


                        if (path.Length > 0)
                            try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }); }
                            catch (Exception ex) { Log.Write("could not reveal the report: " + ex.Message); }
                        try { Process.Start(new ProcessStartInfo(Support.IssueUrl(E, r)) { UseShellExecute = true }); }
                        catch (Exception ex) { Log.Write("could not open the issue form: " + ex.Message); }
                    });
                });
            };
            btnLog.MouseLeftButtonUp += delegate { try { Process.Start(new ProcessStartInfo(Log.Path) { UseShellExecute = true }); } catch (Exception ex) { ShowToast("Cannot open log: " + ex.Message, true); } };
            btnExit.MouseLeftButtonUp += delegate { ExitApp(); };


            btnReset.MouseLeftButtonUp += delegate {
                string ask = "Undo everything " + Program.DisplayName + " changed, then quit?\n\n"
                    + "It re-enables OMEN Gaming Hub's tasks, gives the keyboard back to Windows, puts the refresh rate back, "
                    + "sets the mode to balanced, hands the fans back to the firmware, turns the backlight on, and deletes its "
                    + "own settings and log.\n\n"
                    + "The graphics mode is left as you set it, because changing that needs a restart.\n\n"
                    + "Afterwards you can delete " + Program.AppName + ".exe and nothing of it is left behind.";
                if (MessageBox.Show(IsVisible ? (Window)this : null, ask, Program.DisplayName,
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

                bool alsoDriver = E.S.DriverInstalledBySeal && !E.Hw.IsDemo && E.DriverInstalled
                    && MessageBox.Show(IsVisible ? (Window)this : null,
                        "Also remove the driver " + Program.DisplayName + " installed?\n\nSay No if another program (FanControl, LibreHardwareMonitor) uses it.",
                        Program.DisplayName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                btnReset.Text = "resetting...";
                Slow(delegate {
                    string what = "";
                    try { what = E.FactoryReset(); } catch (Exception ex) { Log.Write("factory reset: " + ex.Message); }
                    if (alsoDriver) {
                        try { if (E.RemoveDriver(false)) what += "  - removed the driver\n"; } catch (Exception ex) { Log.Write("reset driver: " + ex.Message); }
                    }
                    Dispatcher.BeginInvoke((Action)delegate {
                        try { SetAutostart(false); } catch (Exception ex) { Log.Write("reset autostart: " + ex.Message); }
                        resetting = true;
                        string folder = "";
                        try { folder = System.IO.Path.GetDirectoryName(Log.Path); } catch { }

                        if (!IsVisible) { try { Show(); WindowState = WindowState.Normal; Activate(); } catch { } }
                        MessageBox.Show(IsVisible ? (Window)this : null,
                            (what.Length > 0 ? "Done:\n\n" + what + "\n" : "Done.\n\n")
                                + Program.DisplayName + " closes now. Delete " + Program.AppName + ".exe when it does.",
                            Program.DisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                        if (folder.Length > 0)
                            try { Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true }); } catch { }
                        ExitApp();
                    });
                });
            };


            scroll.PreviewMouseWheel += delegate(object o, MouseWheelEventArgs e) {
                e.Handled = true;
                scrollTo = Math.Max(0, Math.Min(scroll.ScrollableHeight, scrollTo - e.Delta / 120.0 * 54));
                if (!scrolling) { scrolling = true; CompositionTarget.Rendering += ScrollTick; }
            };
            scroll.ScrollChanged += delegate { if (!scrolling) scrollTo = scroll.VerticalOffset; };
        }
        double scrollTo;
        bool scrolling;
        void ScrollTick(object o, EventArgs e) {
            double at = scroll.VerticalOffset, d = scrollTo - at;
            if (Math.Abs(d) < 0.6) { scroll.ScrollToVerticalOffset(scrollTo); scrolling = false; CompositionTarget.Rendering -= ScrollTick; return; }
            scroll.ScrollToVerticalOffset(at + d * 0.28);
        }

        void GuardText() {
            var g = E.P.Guard;
            SyncGuardLine();
            txtMaxCoolSub.Text = "Below " + g.MaxFanCoolBelow + "° for " + (g.MaxFanCoolSeconds / 60) + " minutes";
            txtGuardNote.Text = Program.DisplayName + " forces " + (E.GuardLevel > 0 ? E.Rpm(E.GuardLevel) : "max fan") + " above " + E.GuardCpuHot + "° CPU";
        }

        bool ConfirmGuardOff() {
            return MessageBox.Show(IsVisible ? (Window)this : null,
                "Turn the thermal guard off?\n\nThe guard forces both fans to maximum when the CPU passes " + E.P.Guard.CpuHot + "°, the chassis sensor passes " + E.P.Guard.ChassisHot + "°, or the fans read stalled while the machine is warm. With it off, nothing in " + Program.DisplayName + " will step in.\n\nTurn it off?",
                Program.DisplayName, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
        void BuildIcons() {
            for (int i = 0; i < 3; i++) icons[i] = MakeIcon(Ui.ModeColor(i), 32);
        }
        void SetAppIcon(int mode) {
            try {
                if (appIcons[mode] == null)
                    using (var big = MakeIcon(Ui.ModeColor(mode), 256))
                        appIcons[mode] = Imaging.CreateBitmapSourceFromHIcon(big.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                Icon = appIcons[mode];
            } catch { }
        }


        public static SD.Icon MakeIcon(Color c, int size) {
            using (var bmp = DrawMark(SD.Color.FromArgb(c.R, c.G, c.B), size)) return SD.Icon.FromHandle(bmp.GetHicon());
        }
        static SD.Bitmap cachedMarkBmp;
        static SD.Bitmap GetMarkBitmap() {
            if (cachedMarkBmp != null) return cachedMarkBmp;
            try {
                using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Seal.brand.mark.png")) {
                    if (st != null) {
                        using (var img = SD.Image.FromStream(st)) cachedMarkBmp = new SD.Bitmap(img);
                        return cachedMarkBmp;
                    }
                }
            } catch { }
            try {
                string f = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "brand", "mark-512.png");
                if (System.IO.File.Exists(f)) {
                    using (var img = SD.Image.FromFile(f)) cachedMarkBmp = new SD.Bitmap(img);
                    return cachedMarkBmp;
                }
            } catch { }
            return null;
        }

        static ImageSource cachedMarkImgSource;
        static ImageSource GetMarkImageSource() {
            if (cachedMarkImgSource != null) return cachedMarkImgSource;
            try {
                using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Seal.brand.mark.png")) {
                    if (st != null) {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = st;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        cachedMarkImgSource = bi;
                        return cachedMarkImgSource;
                    }
                }
            } catch { }
            try {
                string f = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "brand", "mark-512.png");
                if (System.IO.File.Exists(f)) {
                    var bi = new BitmapImage(new Uri(f));
                    bi.Freeze();
                    cachedMarkImgSource = bi;
                    return cachedMarkImgSource;
                }
            } catch { }
            return null;
        }

        public static SD.Bitmap DrawMark(SD.Color c, int size) { return DrawMark(c, size, null); }

        public static SD.Bitmap DrawMark(SD.Color c, int size, string label) {
            var bmp = new SD.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = SD.Graphics.FromImage(bmp)) {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(SD.Color.Transparent);

                var mark = GetMarkBitmap();
                if (mark != null) {
                    float pad = (size <= 24) ? 0.5f : (size <= 48) ? 1.5f : (size * 0.04f);
                    g.DrawImage(mark, pad, pad, size - pad * 2, size - pad * 2);
                } else {
                    float mid = size / 2f;
                    float side = size * 0.74f;
                    float radius = side * (label == null ? 0.15f : 0.11f);
                    float half = side * 0.71f;
                    using (var path = RoundSquare(mid, side, radius, mid)) {
                        using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(
                                new SD.PointF(0, mid - half - 1), new SD.PointF(0, mid + half + 1),
                                Shade(c, 0.32f), Shade(c, -0.32f)))
                            g.FillPath(b, path);
                    }
                }

                if (label == null) return bmp;

                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                float em = size * (label.Length >= 3 ? 0.44f : 0.62f);
                float midP = size / 2f;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                using (var family = new SD.FontFamily("Segoe UI")) {
                    path.AddString(label, family, (int)SD.FontStyle.Bold, em, new SD.PointF(0, 0), SD.StringFormat.GenericTypographic);
                    var bounds = path.GetBounds();
                    using (var m = new System.Drawing.Drawing2D.Matrix()) {
                        m.Translate(midP - bounds.X - bounds.Width / 2, midP - bounds.Y - bounds.Height / 2);
                        path.Transform(m);
                    }
                    using (var stroke = new SD.Pen(SD.Color.Black, Math.Max(1.5f, size * 0.06f))) g.DrawPath(stroke, path);
                    using (var fill = new SD.SolidBrush(SD.Color.White)) g.FillPath(fill, path);
                }
            }
            return bmp;
        }

        public static void WriteIco(string path, Color c) {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var blobs = new List<byte[]>();
            var mark = GetMarkBitmap();
            foreach (int s in sizes) {
                using (var bmp = new SD.Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                    using (var g = SD.Graphics.FromImage(bmp)) {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.Clear(SD.Color.Transparent);
                        if (mark != null) {
                            float p = (s <= 24) ? 0.5f : (s <= 48) ? 1.5f : (s * 0.045f);
                            g.DrawImage(mark, p, p, s - p * 2, s - p * 2);
                        } else {
                            using (var m = DrawMark(SD.Color.FromArgb(c.R, c.G, c.B), s))
                                g.DrawImage(m, 0, 0, s, s);
                        }
                    }
                    if (s >= 256) {
                        using (var ms = new System.IO.MemoryStream()) {
                            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            blobs.Add(ms.ToArray());
                        }
                    } else {
                        blobs.Add(Dib32(bmp));
                    }
                }
            }
            using (var fs = System.IO.File.Create(path))
            using (var w = new System.IO.BinaryWriter(fs)) {
                w.Write((short)0);
                w.Write((short)1);
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++) {
                    int s = sizes[i];
                    w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((short)1);
                    w.Write((short)32);
                    w.Write(blobs[i].Length);
                    w.Write(offset);
                    offset += blobs[i].Length;
                }
                foreach (var b in blobs) w.Write(b);
            }
        }

        static byte[] Dib32(SD.Bitmap bmp) {
            int w = bmp.Width, h = bmp.Height, maskRow = ((w + 31) / 32) * 4;
            using (var ms = new System.IO.MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms)) {
                bw.Write(40);
                bw.Write(w);
                bw.Write(h * 2);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(0);
                bw.Write(w * h * 4 + maskRow * h);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                for (int y = h - 1; y >= 0; y--)
                    for (int x = 0; x < w; x++) { var p = bmp.GetPixel(x, y); bw.Write(p.B); bw.Write(p.G); bw.Write(p.R); bw.Write(p.A); }
                bw.Write(new byte[maskRow * h]);
                bw.Flush();
                return ms.ToArray();
            }
        }

        static SD.Color Shade(SD.Color c, float t) {
            int to = t > 0 ? 255 : 0;
            float k = Math.Abs(t);
            return SD.Color.FromArgb(c.A,
                (int)Math.Round(c.R + (to - c.R) * k),
                (int)Math.Round(c.G + (to - c.G) * k),
                (int)Math.Round(c.B + (to - c.B) * k));
        }
        static System.Drawing.Drawing2D.GraphicsPath RoundSquare(float center, float side, float radius, float rotateAbout) {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float x = center - side / 2, y = center - side / 2, d = radius * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + side - d, y, d, d, 270, 90);
            p.AddArc(x + side - d, y + side - d, d, d, 0, 90);
            p.AddArc(x, y + side - d, d, d, 90, 90);
            p.CloseFigure();
            using (var m = new System.Drawing.Drawing2D.Matrix()) { m.RotateAt(45f, new SD.PointF(rotateAbout, rotateAbout)); p.Transform(m); }
            return p;
        }


        readonly List<WF.ToolStripMenuItem> trayFan = new List<WF.ToolStripMenuItem>(), trayGfx = new List<WF.ToolStripMenuItem>();
        WF.ToolStripMenuItem trayLight;
        WF.ToolStripMenuItem Item(string text, Action click) {
            var it = new WF.ToolStripMenuItem(text);
            if (click != null) it.Click += delegate { click(); };
            return it;
        }
        void BuildTray() {
            var menu = new WF.ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
            var head = new WF.ToolStripMenuItem(Program.DisplayName + "   " + Program.Version) { Enabled = false };
            menu.Items.Add(head);
            menu.Items.Add(new WF.ToolStripSeparator());
            for (int i = 0; i < 3; i++) {
                int idx = i;
                var it = Item(Engine.ModeNames[i], delegate { ApplyModeAsync(idx); });
                trayModes[i] = it;
                menu.Items.Add(it);
            }
            menu.Items.Add(new WF.ToolStripSeparator());


            var fanMenu = new WF.ToolStripMenuItem("Fan");
            for (int i = 0; i < Choice.Fan.Length; i++) {
                FanMode m = Choice.FanModes[i];
                var it = Item(Choice.Fan[i], delegate { Bg(delegate { E.SetFan(m, E.S.Fan1, E.S.Fan2, false); }); });
                trayFan.Add(it);
                fanMenu.DropDownItems.Add(it);
            }
            menu.Items.Add(fanMenu);


            var gfxMenu = new WF.ToolStripMenuItem("Screen");
            foreach (int hz in Display.Choices()) {
                int h = hz; var it = Item(hz + " Hz refresh rate", delegate { Bg(delegate { E.SetRefreshRate(h); }); }); it.Tag = hz;
                trayHz.Add(it);
                gfxMenu.DropDownItems.Add(it);
            }
            var gfxModes = new List<int>();
            foreach (int mode in new[] { 0, 1, 3 }) if (E.GpuModeOffered(mode)) gfxModes.Add(mode);
            if (gfxModes.Count >= 2) {
                gfxMenu.DropDownItems.Add(new WF.ToolStripSeparator());
                foreach (int mode in gfxModes) {
                    int m = mode;
                    var it = Item(Engine.GpuModeNames[m] + " graphics", delegate { SwitchGraphics(m); }); it.Tag = m;
                    trayGfx.Add(it);
                    gfxMenu.DropDownItems.Add(it);
                }
            }
            if (gfxMenu.DropDownItems.Count > 0) menu.Items.Add(gfxMenu);


            if (E.Light != null) {
                trayLight = Item("Keyboard lighting", delegate {
                    int fx = E.S.LightEffect;
                    int m = E.S.Light == 0 ? 1 : 0;
                    Bg(delegate { E.SetLight(m, fx, false); });
                });
                menu.Items.Add(trayLight);
            }

            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add(Item("Show " + Program.DisplayName, delegate { ShowPanel(); }));
            menu.Items.Add(Item("Exit", delegate { ExitApp(); }));
            menu.Opening += delegate { RefreshTray(); };
            tray = new WF.NotifyIcon { Icon = icons[1], Text = Program.DisplayName, Visible = true, ContextMenuStrip = menu };
            tray.MouseClick += delegate(object o, WF.MouseEventArgs me) { if (me.Button == WF.MouseButtons.Left) TogglePanel(); };
        }

        void RefreshTray() {
            if (tray == null) return;
            var S = E.S;
            for (int i = 0; i < 3; i++) trayModes[i].Checked = i == E.ModeIndex;
            for (int i = 0; i < trayFan.Count; i++) trayFan[i].Checked = S.Fan == Choice.FanModes[i];
            int hzNow = Display.CurrentHz();
            foreach (var it in trayHz) it.Checked = (int)it.Tag == hzNow;
            int gfx = E.GpuModePending >= 0 ? E.GpuModePending : E.GpuMode;
            foreach (var it in trayGfx) it.Checked = (int)it.Tag == gfx;
            if (trayLight != null) { trayLight.Visible = S.KbdLighting; trayLight.Checked = S.Light != 0; }
        }

        void SwitchGraphics(int m) {
            int current = E.GpuModePending >= 0 ? E.GpuModePending : E.GpuMode;
            if (m == current) return;
            var answer = MessageBox.Show("Switch graphics to " + Engine.GpuModeNames[m] + "?\n\nThe change is written now and takes effect after a restart, the same way OMEN Gaming Hub does it.\n\nRestart now?",
                Program.DisplayName, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) { Refresh(); return; }
            if (!E.SetGpuMode(m)) { Refresh(); return; }
            if (answer == MessageBoxResult.Yes) { try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5 /c \"" + Program.DisplayName + ": graphics mode change\"") { CreateNoWindow = true, UseShellExecute = false }); } catch (Exception ex) { ShowToast("Restart failed: " + ex.Message, true); } }
            else ShowToast(Engine.GpuModeNames[m] + " after the next restart", false);
            Refresh();
        }

        void Wire() {
            foreach (string n in new[] { "HeadHome", "HeadFans", "HeadMemory", "HeadOverlay", "HeadKbd", "HeadSettings", "DragStrip" })
                F<FrameworkElement>(n).MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs me) { if (me.LeftButton == MouseButtonState.Pressed) Drag(); };
            IsVisibleChanged += delegate { UpdateDockState(); };
            StateChanged += delegate { UpdateDockState(); };
            btnClose.Click += delegate { HideToTray(); };
            powerDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            powerDebounce.Tick += delegate {
                powerDebounce.Stop();
                if (slSettingsPower != null) {
                    int off = (int)slSettingsPower.Value;
                    Bg(delegate { E.SetTdpOffset(off, false); });
                }
            };
            if (slSettingsPower != null) {
                slSettingsPower.ValueChanged += delegate {
                    int val = (int)slSettingsPower.Value;
                    txtSettingsPowerVal.Text = "+" + val + " W";
                    if (!syncing) { powerDebounce.Stop(); powerDebounce.Start(); }
                };
            }
        }


        void Navigate(Page p, bool animate) {
            if (p != Page.Settings) StopListening();
            if (p != Page.Memory && scrollingMemory) { scrollingMemory = false; CompositionTarget.Rendering -= ScrollMemoryTick; }
            if (p == cur && pageShown) return;
            bool wasKbd = cur == Page.Keyboard;
            var old = pageShown ? pages[(int)cur] : null;
            cur = p;
            pageShown = true;
            var page = pages[(int)p];
            bool live = animate && screenshotPath == null && IsVisible && old != null && old != page;
            for (int i = 0; i < pages.Length; i++) if (pages[i] != page && !(live && pages[i] == old)) ShowPage(pages[i], false);
            pageHost.Width = p == Page.Keyboard ? KbdPageW : PageW;
            for (int i = 0; i < nav.Length; i++) nav[i].SetSelected(i == (int)p);
            if (p == Page.Keyboard) ApplyEditorState();
            else if (wasKbd) RefreshLighting();
            if (p == Page.Memory) {
                if (scrollMemory != null) {
                    scrollMemory.ScrollToTop();
                    scrollMemoryTo = 0;
                }
                UpdateMemoryLive();
            }
            if (p == Page.Overlay) {
                if (scrollOverlay != null) scrollOverlay.ScrollToTop();
                if (overlayPerfSeg != null) overlayPerfSeg.Select(E.S.OverlayPerfMode, false);
                if (overlayOrientSeg != null) overlayOrientSeg.Select(E.S.OverlayHorizontal ? 0 : 1, false);
            }
            if (p == Page.Settings) {
                scroll.ScrollToTop();
                scrollTo = 0;
                UpdateKeyStatus();
                UpdateUpdateRow();
                txtMachine.Text = E.Hw.IsDemo && screenshotPath == null ? "simulated hardware" : (E.BiosOk ? "board " + E.Board : "firmware unavailable");
            }
            if (p == Page.Fans) RefreshFans(false);

            if (!live) ShowPage(page, true);
            else { page.BeginAnimation(UIElement.OpacityProperty, null); page.Opacity = 0; page.IsHitTestVisible = true; page.RenderTransform = null; page.Visibility = Visibility.Visible; }
            double was = ActualWidth * ActualHeight;
            Morph(animate);
            if (live) CrossFade(old, page, (RailW + pageHost.Width) * Math.Ceiling(NaturalHeight()) > was);


            Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)delegate { PlaceRailPill(animate); });
            Log.Write("page " + p);
        }

        void Remeasure(Page p) {
            if (cur != p || !IsVisible || screenshotPath != null) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate { if (cur == p) Morph(true); });
        }


        static void ShowPage(FrameworkElement page, bool on) {
            page.BeginAnimation(UIElement.OpacityProperty, null);
            page.Opacity = 1;
            page.IsHitTestVisible = true;
            page.RenderTransform = null;
            page.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }

        const int FadeOutMs = 110, FadeInMs = 170, GrowDelayMs = 70, ShrinkDelayMs = 25;
        void CrossFade(FrameworkElement old, FrameworkElement page, bool grow) {
            old.IsHitTestVisible = false;
            var outAn = new DoubleAnimation(old.Opacity, 0, TimeSpan.FromMilliseconds(FadeOutMs)) { FillBehavior = FillBehavior.HoldEnd, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            outAn.Completed += delegate { if (pages[(int)cur] != old) ShowPage(old, false); };
            old.BeginAnimation(UIElement.OpacityProperty, outAn);
            var tt = new TranslateTransform(0, 0);
            page.RenderTransform = tt;
            var delay = TimeSpan.FromMilliseconds(grow ? GrowDelayMs : ShrinkDelayMs);
            var inAn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeInMs)) { BeginTime = delay, FillBehavior = FillBehavior.HoldEnd, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            inAn.Completed += delegate { if (pages[(int)cur] == page) { page.Opacity = 1; page.BeginAnimation(UIElement.OpacityProperty, null); } };
            page.BeginAnimation(UIElement.OpacityProperty, inAn);
            var slide = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(360)) { BeginTime = delay, FillBehavior = FillBehavior.Stop, EasingFunction = new SpringEase() };
            tt.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        const int WM_ENTERSIZEMOVE = 0x0231, WM_EXITSIZEMOVE = 0x0232;
        const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_NOCOPYBITS = 0x0100, SWP_NOOWNERZORDER = 0x0200;


        const double GrowDur = 0.40, GrowBounce = 0.15, ShrinkDur = 0.32, ShrinkBounce = 0.0, MorphMaxSec = 1.2;
        bool morphing, morphGrow, morphTicking, morphSizeMove;
        TimeSpan morphLast;
        long morphWall;
        double morphAge;
        readonly double[] mx = new double[4], mv = new double[4], mt = new double[4];
        readonly int[] mpx = new int[4];

        void Morph(bool animate) {
            if (pageHost == null || dragging) return;
            double w = RailW + pageHost.Width, natural = NaturalHeight();
            if (natural < 100) return;
            var wa = SystemParameters.WorkArea;
            double h = Math.Min(Math.Ceiling(natural), Math.Max(360, wa.Height - 24));
            double l = Left, t = Top;
            bool move = IsVisible && screenshotPath == null && !double.IsNaN(l) && !double.IsNaN(t) && l > -30000;
            if (move) {
                if (l + w > wa.Right - 6) l = Math.Max(wa.Left + 6, wa.Right - 6 - w);
                if (t + h > wa.Bottom - 6) t = Math.Max(wa.Top + 6, wa.Bottom - 6 - h);
            } else { l = double.IsNaN(l) ? 0 : l; t = double.IsNaN(t) ? 0 : t; }
            if (!animate || !IsVisible || screenshotPath != null || hwnd == IntPtr.Zero) {
                StopMorph();
                Width = w;
                Height = h;
                if (move) { if (Math.Abs(l - Left) > 0.5) Left = l; if (Math.Abs(t - Top) > 0.5) Top = t; }
                return;
            }
            if (!morphing) {
                if (Math.Abs(ActualWidth - w) < 0.5 && Math.Abs(ActualHeight - h) < 0.5 && Math.Abs(Left - l) < 0.5 && Math.Abs(Top - t) < 0.5) return;
                mx[0] = Left;
                mx[1] = Top;
                mx[2] = Left + ActualWidth;
                mx[3] = Top + ActualHeight;
                for (int i = 0; i < 4; i++) mv[i] = 0;
                mpx[0] = int.MinValue;
                morphAge = 0;
            } else if (Math.Abs(mt[0] - l) < 0.5 && Math.Abs(mt[1] - t) < 0.5 && Math.Abs(mt[2] - (l + w)) < 0.5 && Math.Abs(mt[3] - (t + h)) < 0.5) return;
            morphGrow = w * h > (mx[2] - mx[0]) * (mx[3] - mx[1]);
            mt[0] = l;
            mt[1] = t;
            mt[2] = l + w;
            mt[3] = t + h;
            morphLast = TimeSpan.Zero;
            morphWall = Stopwatch.GetTimestamp();
            if (!morphing) {
                morphing = true;
                SendMessage(hwnd, WM_ENTERSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
                morphSizeMove = true;
                CompositionTarget.Rendering += MorphTick;
            }
        }


        double NaturalHeight() {
            if (cur == Page.Settings) return SettingsH;
            if (cur == Page.Memory) return MemoryH;
            if (cur == Page.Overlay) return OverlayH;
            var page = pages[(int)cur];
            if (page == null || pageHost.Width <= 0) return 0;
            page.Measure(new Size(pageHost.Width, double.PositiveInfinity));
            return page.DesiredSize.Height;
        }
        void StopMorph() {
            if (!morphing) return;
            morphing = false;
            CompositionTarget.Rendering -= MorphTick;
            if (morphSizeMove) { morphSizeMove = false; SendMessage(hwnd, WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero); }
        }
        void MorphTick(object o, EventArgs e) {
            var re = e as RenderingEventArgs;
            if (re == null || morphTicking || !morphing) return;
            if (re.RenderingTime == morphLast) return;
            double dt;
            if (morphLast == TimeSpan.Zero) dt = Math.Max(1.0 / 120, Math.Min(1.0 / 30, (Stopwatch.GetTimestamp() - morphWall) / (double)Stopwatch.Frequency));
            else dt = Math.Min(0.05, (re.RenderingTime - morphLast).TotalSeconds);
            morphLast = re.RenderingTime;
            morphAge += dt;
            double w0 = 2 * Math.PI / (morphGrow ? GrowDur : ShrinkDur), zeta = 1 - (morphGrow ? GrowBounce : ShrinkBounce);
            bool settled = morphAge >= MorphMaxSec;
            if (!settled) {
                settled = true;
                for (int i = 0; i < 4; i++) {
                    Ui.SpringStep(ref mx[i], ref mv[i], mt[i], dt, w0, zeta);
                    if (Math.Abs(mx[i] - mt[i]) > 0.25 || Math.Abs(mv[i]) > 6) settled = false;
                }
            }
            if (settled) for (int i = 0; i < 4; i++) { mx[i] = mt[i]; mv[i] = 0; }
            morphTicking = true;
            try { ApplyBounds(mx[0], mx[1], mx[2], mx[3]); } finally { morphTicking = false; }
            if (settled) {
                StopMorph();
                Width = mt[2] - mt[0];
                Height = mt[3] - mt[1];
                E.S.WinX = (int)mt[0];
                E.S.WinY = (int)mt[1];
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)delegate { PlaceRailPill(true); });
            }
        }
        void ApplyBounds(double l, double t, double r, double b) {
            var ps = PresentationSource.FromVisual(this);
            if (ps == null || ps.CompositionTarget == null) return;
            var m = ps.CompositionTarget.TransformToDevice;
            int x0 = (int)Math.Round(l * m.M11), y0 = (int)Math.Round(t * m.M22), x1 = (int)Math.Round(r * m.M11), y1 = (int)Math.Round(b * m.M22);
            if (x0 == mpx[0] && y0 == mpx[1] && x1 == mpx[2] && y1 == mpx[3]) return;
            mpx[0] = x0;
            mpx[1] = y0;
            mpx[2] = x1;
            mpx[3] = y1;
            SetWindowPos(hwnd, IntPtr.Zero, x0, y0, x1 - x0, y1 - y0, SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS);
        }

        void SetAccent(int modeIndex, bool animate) {
            Color c = Ui.ModeColor(modeIndex);
            if (!animate) { accentSrc.BeginAnimation(ColorSource.ColorProperty, null); accentSrc.Color = c; }
            else Ui.GlideColor(accentSrc, c, 320);
            if (curveView != null) curveView.Repaint();
        }


        void BuildKeyboard() {
            if (E.Light == null) return;
            var layout = BuildLayout();
            kbdBig = new KeyboardView { Interactive = true, Gap = 5, RowPitch = 39 }; kbdBig.SetLayout(layout); kbdHost.Child = kbdBig;
            kbdBig.KeyClicked += delegate(KeyDef k) { if (k != null) SelectGroup(k); };
            kbdBig.KeyHovered += delegate(KeyDef k) {
                kbdBig.Hover.Clear();
                if (k != null) foreach (int i in Group(k)) kbdBig.Hover.Add(i);
                kbdBig.Repaint();
            };
            kbdModes = new LinkSeg(Choice.Light, 22, 13.5, 4, new[] {
                null,
                null,
                "The zone colours pulse",
                "Every zone through the spectrum together",
                "The spectrum travels across the zones",
                "Hand the keyboard to Windows Dynamic Lighting",
                "Support external keyboards (Cosmic Byte, Razer, Corsair, etc.) with gaming animations"
            });
            F<Border>("KbdModeHost").Child = kbdModes;
            if (E.Light.Inert)
                for (int i = 1; i <= 4; i++) kbdModes.SetEnabled(i, false, "This keyboard offers no lighting interface Seal can drive; see the note below");
            kbdModes.Picked += delegate(int i) {
                int m = Choice.LightMode(i), fx = Choice.LightEffect(i);
                E.S.Light = m;
                if (m == 3) fx = E.S.ExternalEffect;
                else E.S.LightEffect = fx;
                ApplyEditorState();
                Bg(delegate { E.SetLight(m, fx, false); });
            };

            extAnimSeg = new ChipSeg(ExternalKeyboards.AnimationNames, ExternalKeyboards.AnimationTips);
            F<Border>("ExternalAnimHost").Child = extAnimSeg;
            extAnimSeg.Picked += delegate(int idx) {
                E.S.ExternalEffect = idx;
                ApplyEditorState();
                Bg(delegate { E.SetExternalEffect(idx); });
            };
            btnRefreshExt.MouseLeftButtonUp += delegate {
                RefreshExternalDeviceUI(true);
                ShowToast("External keyboards refreshed", false);
            };
            extSpeedDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            extSpeedDebounce.Tick += delegate { extSpeedDebounce.Stop(); int sp = (int)slExtSpeed.Value; Bg(delegate { E.SetExternalSpeed(sp); }); };
            slExtSpeed.ValueChanged += delegate {
                txtExtSpeed.Text = ((int)slExtSpeed.Value).ToString(CultureInfo.InvariantCulture);
                if (!syncing) { extSpeedDebounce.Stop(); extSpeedDebounce.Start(); }
            };
            extLevelDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            extLevelDebounce.Tick += delegate { extLevelDebounce.Stop(); int lv = (int)slExtLevel.Value; Bg(delegate { E.SetExternalLevel(lv); }); };
            slExtLevel.ValueChanged += delegate {
                int lv = (int)slExtLevel.Value;
                txtExtLevel.Text = lv + "%";
                if (kbdBig != null && E.S.Light == 3) { kbdBig.Level = kbdMini.Level = lv / 100.0; kbdBig.Repaint(); kbdMini.Repaint(); }
                if (!syncing) { extLevelDebounce.Stop(); extLevelDebounce.Start(); }
            };

            granSeg = new Seg(new[] { "Key", "Row", "Zone", "All" }, null, null, Seg.Kind.Row);
            F<Border>("GranHost").Child = granSeg;
            bool perKey = E.Light.Kind == LightKind.PerKey && E.Light.Zones > 4;
            granSeg.SetEnabled(0, perKey, "This keyboard lights in zones, not per key");
            granSeg.SetEnabled(1, perKey, "This keyboard lights in zones, not per key");
            gran = perKey ? "Key" : "Zone";
            granSeg.Select(perKey ? 0 : 2, false);
            granSeg.Picked += delegate(int i) { gran = new[] { "Key", "Row", "Zone", "All" }[i]; kbdBig.Selected.Clear(); kbdBig.Hover.Clear(); kbdBig.Repaint(); UpdateSelectionText(); };
            hueBar = new StripPicker { Cells = 36 }; F<Border>("HueHost").Child = hueBar;
            shadeBar = new StripPicker { Cells = 30, Shade = true }; F<Border>("ShadeHost").Child = shadeBar;
            hueBar.Picked += delegate(Rgb c) { Paint(c, false); };
            shadeBar.Picked += delegate(Rgb c) { Paint(c, false); };
            colorDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            colorDebounce.Tick += delegate {
                colorDebounce.Stop();
                Rgb v = curColor;
                if (E.S.Light == 3) {
                    if (E.S.ExternalEffect == 2 || E.S.ExternalEffect == 19) {
                        E.S.ExternalEffect = 0;
                        if (extAnimSeg != null) extAnimSeg.Select(0, false);
                    }
                    Bg(delegate { E.SetExternalColor(v.Hex); });
                }
                else { int[] z = SelectedZones(); Bg(delegate { E.SetLightColor(z, v); }); }
            };
            txtHex.TextChanged += delegate {
                if (syncing) return;
                string t = txtHex.Text.Trim().TrimStart('#');
                Rgb c;
                if (t.Length == 6 && Rgb.TryParse(t, out c)) { hexTyping = true; Paint(c, true); hexTyping = false; }
            };
            levelDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            levelDebounce.Tick += delegate { levelDebounce.Stop(); int lv = (int)slLevel.Value; Bg(delegate { E.SetLightLevel(lv); }); };
            RoutedPropertyChangedEventHandler<double> level = delegate(object o, RoutedPropertyChangedEventArgs<double> ev) {
                bool was = syncing;
                if (!was) { syncing = true; try { if (ReferenceEquals(o, slLevel)) slLevel2.Value = slLevel.Value; else slLevel.Value = slLevel2.Value; } finally { syncing = false; } }
                int v = (int)slLevel.Value;
                txtLevel.Text = v + "%";
                txtLevel2.Text = v + "%";
                if (kbdBig != null) { kbdBig.Level = kbdMini.Level = v / 100.0; kbdBig.Repaint(); kbdMini.Repaint(); }
                if (!was) { levelDebounce.Stop(); levelDebounce.Start(); }
            };
            slLevel.ValueChanged += level;
            slLevel2.ValueChanged += level;
            speedDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            speedDebounce.Tick += delegate { speedDebounce.Stop(); int sp = (int)slSpeed.Value; Bg(delegate { E.SetLightSpeed(sp); }); };
            slSpeed.ValueChanged += delegate {
                txtSpeed.Text = ((int)slSpeed.Value).ToString(CultureInfo.InvariantCulture);
                if (!syncing) { speedDebounce.Stop(); speedDebounce.Start(); }
            };
            btnWinLighting.MouseLeftButtonUp += delegate { try { Process.Start(new ProcessStartInfo("ms-settings:personalization-lighting") { UseShellExecute = true }); } catch (Exception ex) { ShowToast("Cannot open Windows settings: " + ex.Message, true); } };

            E.FrameChanged += delegate(Rgb[] f) { Dispatcher.BeginInvoke((Action)delegate { OnFrame(f); }); };
        }

        List<int> Group(KeyDef k) {
            var l = new List<int>();
            foreach (var x in kbdBig.Keys)
                if (gran == "All" || (gran == "Row" && x.Row == k.Row) || (gran == "Zone" && x.Zone == k.Zone) || (gran == "Key" && x.Index == k.Index)) l.Add(x.Index);
            return l;
        }
        void SelectGroup(KeyDef k) {
            bool add = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (!add) kbdBig.Selected.Clear();
            foreach (int i in Group(k)) kbdBig.Selected.Add(i);
            kbdBig.Repaint();
            UpdateSelectionText();
            SyncPickerFromSelection();
        }
        int[] SelectedZones() {
            var set = new List<int>();
            foreach (var k in kbdBig.Keys) if (kbdBig.Selected.Contains(k.Index) && !set.Contains(k.Zone)) set.Add(k.Zone);
            set.Sort();
            return set.ToArray();
        }
        void UpdateSelectionText() {
            int n = kbdBig.Selected.Count;
            if (n == 0) { txtKeySel.Text = "Click the map to select"; return; }
            if (gran == "All") { txtKeySel.Text = "whole keyboard"; return; }
            if (gran == "Zone") {
                var z = SelectedZones();
                var names = new List<string>();
                foreach (int zz in KeyboardLayouts.DisplayOrder(E.Light.Zones)) if (Array.IndexOf(z, zz) >= 0) names.Add(KeyboardLayouts.ZoneName(E.Light.Zones, zz).ToLowerInvariant());
                txtKeySel.Text = z.Length == E.Light.Zones ? "every zone" : string.Join(" + ", names.ToArray());
                return;
            }
            txtKeySel.Text = n + (n == 1 ? " key selected" : " keys selected");
        }

        void Paint(Rgb c, bool fromHex) {
            if (kbdBig.Selected.Count == 0) { foreach (var k in kbdBig.Keys) kbdBig.Selected.Add(k.Index); UpdateSelectionText(); }
            curColor = c;
            if (E.S.Light == 3) {
                E.S.ExternalColor = c.Hex;
                if (E.S.ExternalEffect == 2 || E.S.ExternalEffect == 19) {
                    E.S.ExternalEffect = 0;
                    if (extAnimSeg != null) extAnimSeg.Select(0, false);
                }
                if (E.S.ExternalEffect == 18) {
                    foreach (int z in SelectedZones()) if (z < E.LightColors.Length) E.LightColors[z] = c;
                    kbdBig.SetColors(E.LightColors, false);
                    kbdMini.SetColors(E.LightColors, false);
                } else {
                    var preview = new Rgb[E.LightColors.Length];
                    for (int i = 0; i < preview.Length; i++) preview[i] = c;
                    kbdBig.SetColors(preview, false);
                    kbdMini.SetColors(preview, false);
                }
            } else {
                foreach (int z in SelectedZones()) if (z < E.LightColors.Length) E.LightColors[z] = c;
                kbdBig.SetColors(E.LightColors, false);
                kbdMini.SetColors(E.LightColors, false);
            }
            SyncSwatch(fromHex);
            colorDebounce.Stop();
            colorDebounce.Start();
        }
        void SyncSwatch(bool fromHex) {
            bool was = syncing;
            syncing = true;
            try {
                bool one = OneColour();
                hexChip.Background = one ? Ui.Brush(curColor) : Ui.Brush("#2C2825");
                if (!fromHex && !hexTyping) txtHex.Text = one ? curColor.Hex.ToLowerInvariant() : "";
                hueBar.Current = curColor;
                hueBar.HasCurrent = one;
                hueBar.Repaint();
                double h, s, v;
                curColor.ToHsv(out h, out s, out v);
                shadeBar.Hue = one ? h : 210;
                shadeBar.Current = curColor;
                shadeBar.HasCurrent = one;
                shadeBar.Repaint();
            } finally { syncing = was; }
        }

        bool OneColour() {
            if (E.S.Light == 3) return true;
            int[] z = SelectedZones();
            if (z.Length == 0) return false;
            for (int i = 1; i < z.Length; i++) {
                if (z[i] >= E.LightColors.Length || z[0] >= E.LightColors.Length) return false;
                var a = E.LightColors[z[0]];
                var b = E.LightColors[z[i]];
                if (a.R != b.R || a.G != b.G || a.B != b.B) return false;
            }
            return true;
        }
        void SyncPickerFromSelection() {
            if (E.S.Light == 3) {
                Rgb c;
                if (Rgb.TryParse(E.S.ExternalColor, out c)) curColor = c;
            } else {
                int[] z = SelectedZones();
                if (z.Length > 0 && E.LightColors.Length > z[0]) curColor = E.LightColors[z[0]];
            }
            SyncSwatch(false);
        }
        void OnFrame(Rgb[] f) {
            if (kbdMini == null) return;
            if (cur == Page.Keyboard && IsVisible) kbdBig.SetColors(f, true);
            if (lightRow.IsMouseOver || miniNeedsFrame) { miniNeedsFrame = false; kbdMini.SetColors(f, true); }
        }

        List<KeyDef> BuildLayout() {
            var layout = KeyboardLayouts.Build(E.Light.Numpad, E.Light.Zones);
            var perKey = E.Light as PerKeyLighting;
            if (perKey != null) KeyboardLayouts.BindLamps(layout, perKey.Device);
            return layout;
        }

        void RefreshExternalDeviceUI(bool force) {
            var devices = ExternalKeyboards.Detect(force);
            if (devices.Count > 0) {
                extStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0xC0, 0x6C));
                txtExtDeviceName.Text = devices[0].Name;
                txtExtDeviceDetail.Text = (devices.Count > 1 ? devices.Count + " keyboards: " : "") + devices[0].Brand + (devices[0].IsLampArray ? " (direct RGB sync)" : " (hardware sync active)");
                txtExtBrandTips.Text = ExternalKeyboards.GetBrandTips(devices[0].Brand);
            } else {
                extStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xF3, 0x82, 0x1D));
                txtExtDeviceName.Text = "External Keyboard (Generic/RGB)";
                txtExtDeviceDetail.Text = "No dedicated USB keyboard detected yet. Plug in your Razer, Cosmic Byte, Corsair, etc.";
                txtExtBrandTips.Text = "Tip: For keyboards with hardware animation controllers (e.g. Cosmic Byte, Redragon), use Fn + Ins / Home / PgUp / Del / End / PgDn or Fn + 1-9 to switch hardware modes alongside Seal's animated preview.";
            }
        }

        void ApplyEditorState() {
            if (kbdBig == null) return;
            var S = E.S;
            int m = S.Light, fx = S.LightEffect;

            bool inert = E.Light.Inert;
            if (inert && m != 3) m = S.Light == 2 ? 2 : 0;
            bool lit = m == 1, pick = lit && (fx == 0 || fx == 1), effect = lit && fx != 0;
            bool ext = m == 3;
            bool extOff = ext && S.ExternalEffect == 16;
            bool extCustom = ext && S.ExternalEffect == 15;
            bool extCanColor = ext && !extOff;
            kbdModes.Select(Choice.OfLight(m, fx), IsVisible && cur == Page.Keyboard);
            selectRow.Visibility = (pick || extCustom) ? Visibility.Visible : Visibility.Collapsed;
            colorEditor.Visibility = (pick || extCanColor) ? Visibility.Visible : Visibility.Collapsed;
            colorEditor.Margin = new Thickness(0, ext ? 0 : 0, 0, ext ? 16 : 0);
            effectEditor.Visibility = effect ? Visibility.Visible : Visibility.Collapsed;
            effectEditor.Margin = new Thickness(0, pick ? 18 : 0, 0, 0);
            levelInline.Visibility = (pick && !effect) ? Visibility.Visible : Visibility.Collapsed;
            externalKbdEditor.Visibility = ext ? Visibility.Visible : Visibility.Collapsed;
            kbdInfo.Visibility = (lit || ext) ? Visibility.Collapsed : Visibility.Visible;
            btnWinLighting.Visibility = m == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (ext) {
                extAnimSeg.Select(Math.Max(0, Math.Min(ExternalKeyboards.AnimationNames.Length - 1, S.ExternalEffect)), false);
                RefreshExternalDeviceUI(false);
                Rgb c;
                if (Rgb.TryParse(S.ExternalColor, out c)) curColor = c;
                SyncSwatch(false);
            }


            txtKbdInfo.Text = inert && m != 2 && m != 3
                ? "Seal cannot light this keyboard. HP's firmware interface answers for per-key boards but does nothing, and this keyboard offers no lighting interface of its own." + (WinLighting.KeyboardFound ? " Windows Dynamic Lighting can still light it." : "") + " Run Seal.exe --lamps and open an issue with what it prints."
                : m == 0 ? "The backlight is off. Pick a mode to turn it back on, or press the keyboard backlight key."
                : (WinLighting.Present || E.Hw.IsDemo ? "Windows Dynamic Lighting has the keyboard. Its colours and effects come from Windows settings."
                                                      : "No Dynamic Lighting device for this keyboard was found; Windows cannot drive it.");
            kbdBig.Selectable = pick || ext;
            kbdBig.Off = m == 0 || extOff;
            kbdBig.WindowsOwned = m == 2;
            kbdBig.Smooth = effect || (ext && !extCustom);
            kbdMini.Off = m == 0 || extOff;
            kbdMini.WindowsOwned = m == 2;
            kbdMini.Smooth = effect || (ext && !extCustom);
            kbdBig.Level = kbdMini.Level = (ext ? S.ExternalLevel : S.LightLevel) / 100.0;
            if (!pick && !ext) { kbdBig.Selected.Clear(); kbdBig.Hover.Clear(); }
            string fxName = new[] { "static", "breathe", "cycle", "wave" }[Math.Max(0, Math.Min(3, fx))];
            string extName = ExternalKeyboards.AnimationNames[Math.Max(0, Math.Min(ExternalKeyboards.AnimationNames.Length - 1, S.ExternalEffect))].ToLowerInvariant();

            txtKbdStatus.Text = ext ? "external · " + extName : inert && m != 2 ? "per-key · no interface we can drive" : m == 2 ? "windows lighting" : m == 0 ? "backlight off" : fx >= 2 ? "firmware effect · colour fixed" : E.Light.Describe + " · " + fxName;
            UpdateSelectionText();
            miniNeedsFrame = true;
            kbdBig.Repaint();
            kbdMini.Repaint();
            Remeasure(Page.Keyboard);
        }
        void RefreshLighting() {
            if (E.Light == null || kbdMini == null) return;
            var S = E.S;
            ApplyEditorState();
            if (!(S.Light == 1 && S.LightEffect != 0) && S.Light != 3) { kbdMini.SetColors(E.LightColors, false); kbdBig.SetColors(E.LightColors, false); }
            bool was = syncing;
            syncing = true;
            try {
                slLevel.Value = slLevel2.Value = Math.Max(5, S.LightLevel);
                txtLevel.Text = txtLevel2.Text = S.LightLevel + "%";
                slSpeed.Value = S.LightSpeed;
                txtSpeed.Text = S.LightSpeed.ToString(CultureInfo.InvariantCulture);
                slExtSpeed.Value = S.ExternalSpeed;
                txtExtSpeed.Text = S.ExternalSpeed.ToString(CultureInfo.InvariantCulture);
                slExtLevel.Value = Math.Max(5, S.ExternalLevel);
                txtExtLevel.Text = S.ExternalLevel + "%";
            } finally { syncing = was; }
            if (cur != Page.Keyboard) SyncPickerFromSelection();
            string fxName = new[] { "Static", "Breathe", "Cycle", "Wave" }[Math.Max(0, Math.Min(3, S.LightEffect))];
            string extName = ExternalKeyboards.AnimationNames[Math.Max(0, Math.Min(ExternalKeyboards.AnimationNames.Length - 1, S.ExternalEffect))];
            txtLightSub.Text = S.Light == 3 ? "External · " + extName : S.Light == 2 ? "Windows Dynamic Lighting" : S.Light == 0 ? "Off" : fxName + " · " + E.Light.Describe;
        }


        void RefreshFans(bool animate) {
            var S = E.S;
            FanMode f = S.Fan;
            bool linked = S.Cur.CurveLinked;
            bool was = syncing;
            syncing = true;
            try {
                fanSeg.Select(Choice.Of(f), animate && IsVisible);
                curveBlock.Visibility = f == FanMode.Auto || f == FanMode.Custom ? Visibility.Visible : Visibility.Collapsed;
                maxBlock.Visibility = f == FanMode.Max ? Visibility.Visible : Visibility.Collapsed;
                manualBlock.Visibility = f == FanMode.Manual ? Visibility.Visible : Visibility.Collapsed;
                optsAuto.Visibility = f == FanMode.Auto ? Visibility.Visible : Visibility.Collapsed;
                optsCurve.Visibility = f == FanMode.Custom ? Visibility.Visible : Visibility.Collapsed;
                optsMax.Visibility = f == FanMode.Max ? Visibility.Visible : Visibility.Collapsed;
                optsManual.Visibility = f == FanMode.Manual ? Visibility.Visible : Visibility.Collapsed;
                tgEcoCool2.IsChecked = S.EcoCool;
                tgMaxCool.IsChecked = S.MaxBackWhenCool;
                tgManualLink.IsChecked = S.ManualLinked;
                tgLink.IsChecked = linked;
                for (int i = 0; i < stopAfterSeg.Count; i++) if ((int)stopAfterSeg.Tags[i] == S.MaxStopAfterMin) stopAfterSeg.Select(i, animate && IsVisible);
                if (linked || f != FanMode.Custom) curveGpu = false;
                curveWhichHost.Visibility = f == FanMode.Custom && !linked ? Visibility.Visible : Visibility.Collapsed;
                curveWhich.Select(curveGpu ? 1 : 0, animate && IsVisible);
                int floor = S.Cur.CurveFloor <= E.P.Curve.Floor ? E.P.Curve.Floor : S.Cur.CurveFloor;
                slFloor.Value = Math.Min(slFloor.Maximum, floor);
                txtFloor.Text = floor <= E.P.Curve.Floor ? "off" : Pct(floor);
                slRamp.Value = S.Cur.CurveRamp;
                txtRamp.Text = S.Cur.CurveRamp + " s";
                curveView.ReadOnly = f != FanMode.Custom;
                curveView.UserFloor = f == FanMode.Custom && floor > E.P.Curve.Floor ? floor : 0;
                if (f == FanMode.Auto) {
                    curveView.Levels = E.VendorCurveAt(false);
                    txtCurveTitle.Text = "This model's curve";
                    txtCurveHint.Text = "read-only";
                } else if (f == FanMode.Custom) {
                    if (!curveDebounce.IsEnabled) curveView.Levels = (int[])(curveGpu ? S.Cur.GpuCurveLevels : S.Cur.CurveLevels).Clone();
                    txtCurveTitle.Text = curveGpu ? "GPU curve" : "CPU curve";
                    txtCurveHint.Text = "drag a point · shift-drag moves all";
                }
                slFan1.Value = S.Fan1;
                slFan2.Value = S.Fan2;
                txtFan1.Text = Pct(S.Fan1);
                txtFan2.Text = Pct(S.Fan2);
                manPct1.Text = Pct(S.Fan1).TrimEnd('%');
                manPct2.Text = Pct(S.Fan2).TrimEnd('%');
                manSub1.Text = E.Rpm(S.Fan1) + " · CPU fan";
                manSub2.Text = E.Rpm(S.Fan2) + " · GPU fan";
                UpdateCurveLive();
                UpdateMaxBlock();
                UpdateFanStatus();
                UpdateFanFooter();
            } finally { syncing = was; }
            Remeasure(Page.Fans);
        }
        void UpdateMaxBlock() {
            if (E.S.Fan != FanMode.Max) return;
            int f1 = lastFans != null && lastFans[0] > 0 ? lastFans[0] : 0, f2 = lastFans != null && lastFans[1] > 0 ? lastFans[1] : 0;
            maxFan1.Text = Level(f1);
            maxFan2.Text = Level(f2);
            maxFan1Sub.Text = "CPU fan" + (f1 > 0 ? " · " + Pct(f1) : "");
            maxFan2Sub.Text = "GPU fan" + (f2 > 0 ? " · " + Pct(f2) : "");
            maxTemp.Text = double.IsNaN(E.CpuTemp) ? "--" : E.CpuTemp.ToString("0", CultureInfo.InvariantCulture);
            maxTempSub.Text = "CPU · " + Trend();
            var left = E.MaxLeft;
            if (E.S.MaxStopAfterMin > 0 && left > TimeSpan.Zero) { maxMins.Text = ((int)Math.Ceiling(left.TotalMinutes)).ToString(CultureInfo.InvariantCulture); maxMinsUnit.Text = " min"; maxMinsSub.Text = "Until it stops"; }
            else { maxMins.Text = E.MaxMinutes.ToString(CultureInfo.InvariantCulture); maxMinsUnit.Text = " min"; maxMinsSub.Text = "Running at max"; }
        }
        string Trend() {
            if (tempTrail.Count < 4) return "steady";
            double a = 0, b = 0;
            int half = tempTrail.Count / 2;
            for (int i = 0; i < half; i++) a += tempTrail[i];
            for (int i = half; i < tempTrail.Count; i++) b += tempTrail[i];
            double d = b / (tempTrail.Count - half) - a / half;
            return d > 0.8 ? "rising" : d < -0.8 ? "falling" : "steady";
        }
        void UpdateFanFooter() {
            var S = E.S;
            bool link = S.Fan == FanMode.Auto || S.Fan == FanMode.Custom;
            btnFanAction.Visibility = link ? Visibility.Visible : Visibility.Collapsed;
            txtFanRight.Visibility = link ? Visibility.Collapsed : Visibility.Visible;
            btnFanAction.Text = S.Fan == FanMode.Auto ? "Edit as curve" : "Reset curve";
            if (S.Fan == FanMode.Max) {
                txtFanApplied.Text = "Loud" + (ShowChassis(lastBiosTemp) ? " · ambient " + lastBiosTemp + "°" : "");
                txtFanRight.Text = "Ctrl+Alt+M toggles";
            } else {
                txtFanApplied.Text = E.GuardActive ? "Thermal guard: max fan until cool" : "Applied to " + E.ModeName;
                txtFanRight.Text = "CPU " + (double.IsNaN(E.CpuTemp) ? "--" : E.CpuTemp.ToString("0") + "°") + " · GPU " + (double.IsNaN(E.GpuTemp) ? "--" : E.GpuTemp.ToString("0") + "°");
            }
        }
        void UpdateCurveLive() {
            var S = E.S;
            double t;
            int lvl;
            if (S.Fan == FanMode.Custom && !S.Cur.CurveLinked && curveGpu) { t = E.GpuTemp; lvl = E.AutoLevel2; }
            else if (S.Fan == FanMode.Custom && S.Cur.CurveLinked) { t = double.IsNaN(E.CpuTemp) ? E.GpuTemp : double.IsNaN(E.GpuTemp) ? E.CpuTemp : Math.Max(E.CpuTemp, E.GpuTemp); lvl = E.AutoLevel1; }
            else { t = E.CpuTemp; lvl = E.AutoLevel1; }
            curveView.LiveTemp = t;
            curveView.LiveLevel = lvl;
            curveView.Repaint();
        }
        void UpdateFanStatus() {
            if (lastFans == null) { txtFansStatus.Text = ""; return; }
            string rpm = Level(lastFans[0]) + " / " + E.Rpm(Math.Max(0, lastFans[1]));
            int pct = lastFans[0] > 0 && E.P.Curve.Ceiling > 0 ? (int)Math.Round(100.0 * Math.Max(lastFans[0], lastFans[1]) / E.P.Curve.Ceiling) : -1;
            txtFansStatus.Text = rpm + (pct >= 0 ? " · " + pct + "%" : "");
        }
        string Pct(int level) { return E.Percent(level); }

        string Level(int level) {
            if (level < 0) return "--";
            return E.P.RpmPerLevel > 0 ? (level * E.P.RpmPerLevel).ToString(CultureInfo.InvariantCulture) : E.Percent(level).TrimEnd('%');
        }


        void BuildRefreshRates() {
            int[] rates = Display.Choices();
            if (rates.Length < 2) return;
            hzRow.Visibility = Visibility.Visible;
            lowHzRow.Visibility = Visibility.Visible;
            var names = new string[rates.Length];
            var tags = new object[rates.Length];
            for (int i = 0; i < rates.Length; i++) { names[i] = i == rates.Length - 1 ? rates[i] + " Hz" : rates[i].ToString(CultureInfo.InvariantCulture); tags[i] = rates[i]; }
            hzSeg = new Seg(names, null, tags, Seg.Kind.Row);
            hzSeg.SetMono(11.5);
            F<Border>("HzSegHost").Child = hzSeg;
            hzSeg.Picked += delegate(int i) { int h = (int)hzSeg.Tags[i]; Bg(delegate { E.SetRefreshRate(h); }); };
        }

        void BuildGraphicsModes() {
            int[] order = { 0, 1, 3 };
            var names = new List<string>();
            var tags = new List<object>();
            foreach (int mode in order) if (E.GpuModeOffered(mode)) { names.Add(Engine.GpuModeNames[mode]); tags.Add(mode); }
            if (names.Count < 2) return;
            gfxSeg = new Seg(names.ToArray(), null, tags.ToArray(), Seg.Kind.Row);
            F<Border>("GfxSegHost").Child = gfxSeg;
            gfxRow.Visibility = Visibility.Visible;
            gfxSeg.Picked += delegate(int i) { SwitchGraphics((int)gfxSeg.Tags[i]); };
        }
        void RunKeyCommand() {
            string cmd = E.S.KeyCommand.Trim();
            if (cmd.Length == 0) { ShowToast("OMEN key: no command set (Settings)", true); return; }
            try {
                string file = cmd, args = "";
                if (cmd.StartsWith("\"")) { int q = cmd.IndexOf('"', 1); if (q > 0) { file = cmd.Substring(1, q - 1); args = cmd.Substring(q + 1).Trim(); } }
                else { int sp = cmd.IndexOf(' '); if (sp > 0) { file = cmd.Substring(0, sp); args = cmd.Substring(sp + 1); } }
                Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
                Flash("OMEN key", cmd, E.ModeIndex);
            } catch (Exception ex) { ShowToast("OMEN key command failed: " + ex.Message, true); }
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        int trayTempShown = int.MinValue;
        SD.Icon trayTempIcon;
        void UpdateTrayTemp(double cpu) {
            if (tray == null) return;
            if (!E.S.TrayTemp || double.IsNaN(cpu)) { if (trayTempShown != int.MinValue) { trayTempShown = int.MinValue; try { tray.Icon = icons[E.ModeIndex]; } catch { } } return; }
            int t = (int)Math.Round(cpu);
            int key = t * 4 + E.ModeIndex;
            if (key == trayTempShown) return;
            trayTempShown = key;
            try {
                var old = trayTempIcon;
                trayTempIcon = TempIcon(t, Ui.ModeColor(E.ModeIndex));
                tray.Icon = trayTempIcon;
                if (old != null) { IntPtr h = old.Handle; old.Dispose(); DestroyIcon(h); }
            } catch (Exception ex) { Log.Write("tray temp icon: " + ex.Message); }
        }

        static SD.Icon TempIcon(int temp, Color c) {
            using (var bmp = DrawMark(SD.Color.FromArgb(c.R, c.G, c.B), 32, temp.ToString(CultureInfo.InvariantCulture))) return SD.Icon.FromHandle(bmp.GetHicon());
        }

        void Bg(Action a) { E.Post(a); }


        static void Slow(Action a) { ThreadPool.QueueUserWorkItem(delegate { try { a(); } catch (Exception ex) { Log.Write("slow: " + ex); } }); }


        void Synced(Action a) {
            bool was = syncing;
            syncing = true;
            try { a(); } finally { syncing = was; }
        }


        void OnSwitch(ToggleButton t, Action<bool> set) {
            RoutedEventHandler h = delegate { if (syncing) return; set(t.IsChecked == true); };
            t.Checked += h;
            t.Unchecked += h;
        }

        string ShortModel() {
            string n = E.P.Name ?? "";
            int p = n.IndexOf('(');
            if (p > 0) n = n.Substring(0, p);
            n = n.Replace("HP ", "").Replace("OMEN ", "").Trim();
            return n.Length == 0 ? "OMEN" : n;
        }

        void UpdateKeyStatus() {
            if (E.Learning) return;
            bool learned = E.S.KeyId != 0, seen = E.LastEventTime != DateTime.MinValue;
            bool known = learned || seen || (E.P.Verified && E.KeyId != 0);
            keyDot.Fill = Ui.Brush(known ? Ui.Ok : Ui.Warn);
            txtKeyInfo.Text = learned ? "OMEN key learned" : known ? "OMEN key auto-detected" : "OMEN key not detected";
            btnLearn.Text = known ? "Relearn" : "Learn";
        }
        void UpdateUpdateRow() {
            string staged = E.Staged;
            bool newer = E.UpdateAvailable;

            if (updateTitleFor != (staged ?? "")) {
                updateTitleFor = staged ?? "";
                txtUpdateTitle.Inlines.Clear();
                if (staged != null) {
                    txtUpdateTitle.Inlines.Add(new Run("Restart " + Program.DisplayName) { Foreground = accent });
                    txtUpdateTitle.Inlines.Add(new Run(" to update") { Foreground = Ui.TextB });
                } else txtUpdateTitle.Inlines.Add(new Run("Check for updates") { Foreground = Ui.TextB });
                updateText.Cursor = staged != null ? Cursors.Hand : null;
            }
            txtUpdate.Text = staged != null
                ? Program.Version + " → " + staged
                : Program.Version + " · " + Update.Ago(E.LastUpdateCheck) + (newer ? " · " + E.LatestVersion + " available" : "");


            txtUpdate.Foreground = (staged == null && newer) ? (Brush)accent : Ui.Desc;
            btnUpdate.Text = staged != null ? "Changelog" : newer ? "Download" : "Check now";
            if (navUpdate != null) {
                var want = staged != null ? Visibility.Visible : Visibility.Collapsed;
                if (navUpdate.Visibility != want) {
                    navUpdate.Visibility = want;
                    navUpdate.ToolTip = staged == null ? "Update available" : "Update to " + staged + " is ready";

                    Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)delegate { PlaceRailPill(true); });
                }
            }
        }


        void UpdateDriverRow() {
            var S = E.S;
            bool demo = E.Hw.IsDemo;
            bool installed = E.DriverInstalled;
            string title = "Hardware driver", sub, link = null, name = null;
            bool showSwitch = installed && !E.DriverBusy;


            bool intel = demo || CpuRegisters.IsIntel;
            string gains = (intel ? "more accurate CPU temperature, power limits & throttle reasons" : "a more accurate CPU temperature")
                + ((E.P.DriverFor & DriverFor.FanLevels) != 0 ? ", and fan levels on this board" : "");
            if (E.DriverBusy) { sub = E.DriverProgress; driverState = DriverState.Busy; }


            else if (S.DriverRestartPending && !E.DriverReady) { sub = "Installed · restart Windows to finish"; link = "Restart now"; driverState = DriverState.RestartPending; }
            else if (!installed) { sub = "Adds " + gains; link = "Install"; driverState = DriverState.NotInstalled; }
            else if (!S.DriverUse) { sub = "Off · adds " + gains; driverState = DriverState.Off; }
            else if (E.DriverOutdated) { name = DriverName(); sub = " · needs " + PawnIo.MinVersion + " or newer"; link = "Update"; driverState = DriverState.Outdated; }
            else if (E.DriverReady) {


                name = DriverName();
                string does = E.Route == Engine.FanRoute.Ec ? "fan levels" : null;
                if (E.Cpu != null) does = (does != null ? does + " and " : "") + (intel ? "CPU temperature and power limits" : "CPU temperature");
                sub = " · " + (demo ? "simulated" : does ?? "open");
                driverState = DriverState.Ready;
                if (!demo && S.DriverInstalledBySeal) link = "Remove";
            } else { title = "Hardware driver not detected"; sub = E.DriverWhy; link = "Troubleshoot"; driverState = DriverState.Broken; }
            string key = title + "|" + (name ?? "") + sub + "|" + (link ?? "") + "|" + showSwitch;
            if (key != driverRowFor) {
                driverRowFor = key;
                txtDriverTitle.Text = title;
                if (name != null) DriverSubLinked(name, sub); else txtDriverSub.Text = sub;
                btnDriver.Text = link ?? "";
                btnDriver.Visibility = link != null ? Visibility.Visible : Visibility.Collapsed;


                btnDriver.ToolTip =
                    driverState == DriverState.NotInstalled || driverState == DriverState.Outdated
                        ? "Same driver FanControl, LibreHardwareMonitor and a dozen other hardware tools install. Removable any time."
                    : driverState == DriverState.Ready
                        ? "Safe, " + Program.DisplayName + " falls back to the temperature Windows reports."
                    : null;
                tgDriver.Visibility = showSwitch ? Visibility.Visible : Visibility.Collapsed;
            }
            tgDriver.IsChecked = demo || S.DriverUse;

            string nudge = E.DriverNudge;
            var want = nudge != null ? Visibility.Visible : Visibility.Collapsed;
            if (nudge != null) {
                txtDriverNudge.Text = nudge;
                bool restart = S.DriverRestartPending;
                txtDriverNudgeSub.Text = restart ? "The driver is installed and waits for a restart" : "Your firmware refuses fan levels through BIOS commands. " + Program.DisplayName + " can set them through a driver.";
                btnDriverNudge.Text = restart ? "Restart now" : "Install driver";
            }
            if (driverBanner.Visibility != want) { driverBanner.Visibility = want; if (cur == Page.Home) Remeasure(cur); }
        }

        string DriverName() {
            Version v = E.DriverVersion;
            return v == null ? "PawnIO" : "PawnIO " + (v.Build >= 0 ? v.ToString(3) : v.ToString());
        }


        void DriverSubLinked(string name, string rest) {
            var link = new Hyperlink(new Run(name)) { Foreground = accent, TextDecorations = null, Cursor = Cursors.Hand, ToolTip = PawnIo.SourceUrl };
            link.Click += delegate { OpenUrl(PawnIo.SourceUrl); };
            link.MouseEnter += delegate { link.TextDecorations = TextDecorations.Underline; };
            link.MouseLeave += delegate { link.TextDecorations = null; };
            txtDriverSub.Inlines.Clear();
            txtDriverSub.Inlines.Add(link);
            txtDriverSub.Inlines.Add(new Run(rest));
        }
        void OpenUrl(string url) {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { ShowToast("Cannot open " + url + ": " + ex.Message, true); }
        }
        void DriverAction() {


            if (driverState == DriverState.NotInstalled || driverState == DriverState.Outdated) InstallDriver();
            else if (driverState == DriverState.RestartPending) RestartWindows("the driver");
            else if (driverState == DriverState.Broken) DriverCheck();

            else if (driverState == DriverState.Ready) Slow(delegate { E.RemoveDriver(); });
        }
        void InstallDriver() {
            if (E.Hw.IsDemo) { ShowToast("Simulated hardware: nothing to install", true); return; }
            if (E.DriverBusy) return;


            Slow(delegate { E.InstallDriver(); });
        }
        void DriverCheck() {
            btnDriver.Text = "checking…";
            Slow(delegate {
                string d;
                try { d = Support.DriverReport(E); } catch (Exception ex) { d = "driver check failed: " + ex.Message; }
                Log.Write(d);
                Dispatcher.BeginInvoke((Action)delegate {
                    driverRowFor = "?";
                    UpdateDriverRow();
                    bool copied = false;
                    try { Clipboard.SetText(d); copied = true; } catch { }
                    txtDiag.Text = d.TrimEnd();
                    txtDiag.Visibility = Visibility.Visible;
                    Remeasure(cur);
                    ShowToast(copied ? "Copied - paste it into a GitHub issue or Discord" : "See below", false);
                });
            });
        }
        void RestartWindows(string why) {
            if (MessageBox.Show(IsVisible ? (Window)this : null, "Restart Windows now to finish installing " + why + "?", Program.DisplayName,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5 /c \"" + Program.DisplayName + ": finishing the driver install\"") { CreateNoWindow = true, UseShellExecute = false }); }
            catch (Exception ex) { ShowToast("Restart failed: " + ex.Message, true); }
        }

        void ShowUpdateRow() { ShowRow(updateRow); }
        void ShowDriverRow() { ShowRow(driverRow); }
        void ShowRow(FrameworkElement row) {
            Navigate(Page.Settings, true);

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate {
                try {
                    var content = scroll.Content as FrameworkElement;
                    if (content == null) return;
                    double y = row.TranslatePoint(new Point(0, 0), content).Y;
                    scrollTo = Math.Max(0, Math.Min(scroll.ScrollableHeight, y - 96));
                    if (screenshotPath != null) { scroll.ScrollToVerticalOffset(scrollTo); return; }
                    if (!scrolling) { scrolling = true; CompositionTarget.Rendering += ScrollTick; }
                } catch { }
            });
        }
        void OpenReleases() {
            try { Process.Start(new ProcessStartInfo(Update.ReleasesUrl) { UseShellExecute = true }); }
            catch (Exception ex) { ShowToast("Cannot open the releases page: " + ex.Message, true); }
        }
        void DoUpdate() {
            string err;
            if (!E.StartUpdate(out err)) { ShowToast("Could not install the update: " + err, true); return; }

            quietExit = true;
            ExitApp();
        }

        void ApplyModeAsync(int idx) {
            SelectMode(idx, true);
            Bg(delegate { E.SetMode(idx, false); });
        }
        void SelectMode(int idx, bool animate) {
            modeSeg.Select(idx, animate && IsVisible);
            txtHomeTitle.Text = Engine.ModeNames[idx];
            if (shownMode != idx) { shownMode = idx; SetAccent(idx, animate && IsVisible); SetAppIcon(idx); try { tray.Icon = icons[idx]; } catch { } }
        }

        public void Refresh() {
            PollRate();
            bool wasSyncing = syncing;
            syncing = true;
            try {
                var S = E.S;
                int mi = E.ModeIndex;
                SelectMode(mi, true);
                if (slSettingsPower != null) {
                    slSettingsPower.Value = S.TdpOffset;
                    txtSettingsPowerVal.Text = "+" + S.TdpOffset + " W";
                }
                GpuLevel g = E.EffectiveGpu;
                string gpuName = g == GpuLevel.Max ? "GPU max" : g == GpuLevel.Boost ? "GPU boost" : "GPU base";

                txtHomeStatus.Text = (E.P.HasGpuPower ? gpuName : E.P.HasPowerGain ? E.CurrentTdp + " W" : "")
                    + (ShowChassis(lastBiosTemp) ? " · ambient " + lastBiosTemp + "°" : "");
                fanLinks.SetText(2, S.Fan == FanMode.Custom ? "Curve" : "Manual");
                fanLinks.Select(S.Fan == FanMode.Auto ? 0 : S.Fan == FanMode.Max ? 1 : 2, IsVisible);
                if (pollSeg != null) pollSeg.Select(S.PollMs <= 500 ? 0 : S.PollMs <= 1000 ? 1 : 2, IsVisible && cur == Page.Settings);
                if (gpuSeg != null) { gpuSeg.Select(S.GpuAuto ? 3 : (int)g, IsVisible && cur == Page.Settings); txtGpuSub.Text = S.GpuAuto ? "Follows the mode" : g == GpuLevel.Max ? "Custom TGP + PPAB" : g == GpuLevel.Boost ? "PPAB" : "Base TGP"; }
                RefreshLighting();
                bool showKbd = E.Light != null && S.KbdLighting;
                if (nav[4] != null) nav[4].Visibility = showKbd ? Visibility.Visible : Visibility.Collapsed;
                if (lightRow != null && E.Light != null) lightRow.Visibility = showKbd ? Visibility.Visible : Visibility.Collapsed;
                if (tgKbdLighting != null) tgKbdLighting.IsChecked = S.KbdLighting;
                if (cur == Page.Keyboard && !showKbd) Navigate(Page.Home, true);
                if (tgOverlayEnable != null) tgOverlayEnable.IsChecked = S.Overlay;
                if (tgOverlayPin != null) tgOverlayPin.IsChecked = S.OverlayPinned;
                if (slOverlayOpacity != null) {
                    slOverlayOpacity.Value = S.OverlayOpacity;
                    txtOverlayOpacity.Text = S.OverlayOpacity + "%";
                }
                if (tgOverlayCpu != null) tgOverlayCpu.IsChecked = S.OverlayShowCpu;
                if (tgOverlayGpu != null) tgOverlayGpu.IsChecked = S.OverlayShowGpu;
                if (tgOverlayRam != null) tgOverlayRam.IsChecked = S.OverlayShowRam;
                if (tgOverlayFps != null) tgOverlayFps.IsChecked = S.OverlayShowFps;
                if (tgOverlayUpload != null) tgOverlayUpload.IsChecked = S.OverlayShowUpload;
                if (tgOverlayDownload != null) tgOverlayDownload.IsChecked = S.OverlayShowDownload;
                if (tgOverlayPerf != null) tgOverlayPerf.IsChecked = S.OverlayShowPerf;
                if (overlayPerfSeg != null) overlayPerfSeg.Select(S.OverlayPerfMode, false);
                if (overlayOrientSeg != null) overlayOrientSeg.Select(S.OverlayHorizontal ? 0 : 1, false);
                if (S.Overlay && (overlayWin == null || !overlayWin.IsVisible)) SyncOverlayWindow(true);
                else if (!S.Overlay && overlayWin != null && overlayWin.IsVisible) SyncOverlayWindow(false);
                if (overlayPerfWin != null && overlayPerfWin.IsVisible) overlayPerfWin.UpdateState();
                if (cur == Page.Fans) RefreshFans(true);
                keySeg.Select(Choice.Of(S.Key), IsVisible && cur == Page.Settings);
                keyCmdRow.Visibility = S.Key == KeyAction.Run ? Visibility.Visible : Visibility.Collapsed;
                if (!txtKeyCmd.IsKeyboardFocused) txtKeyCmd.Text = S.KeyCommand;
                tgLowHzBattery.IsChecked = S.LowHzOnBattery;
                tgTrayTemp.IsChecked = S.TrayTemp;
                tgGuard.IsChecked = S.Guard;
                tgUpdateAuto.IsChecked = S.UpdateOnLaunch;
                int hzNow = Display.CurrentHz();
                if (hzSeg != null) for (int i = 0; i < hzSeg.Count; i++) if ((int)hzSeg.Tags[i] == hzNow) hzSeg.Select(i, IsVisible && cur == Page.Settings);
                foreach (var m in trayHz) m.Checked = (int)m.Tag == hzNow;
                int gfx = E.GpuModePending >= 0 ? E.GpuModePending : (E.GpuMode >= 0 ? E.GpuMode : 0);
                if (gfxSeg != null) for (int i = 0; i < gfxSeg.Count; i++) if ((int)gfxSeg.Tags[i] == gfx) gfxSeg.Select(i, IsVisible && cur == Page.Settings);
                txtGfxSub.Text = E.GpuModePending >= 0 && E.GpuModePending != E.GpuMode ? Engine.GpuModeNames[E.GpuModePending] + " after the next restart" : "Takes effect after a restart";
                UpdateKeyStatus();
                UpdateUpdateRow();
                UpdateDriverRow();
                tgSuppress.IsChecked = S.SuppressOgh;
                tgHotkeys.IsChecked = S.Hotkeys;
                string omen = S.Key == KeyAction.Show ? Program.DisplayName : S.Key == KeyAction.Cycle ? "cycles" : S.Key == KeyAction.MaxFan ? "max fan" : S.Key == KeyAction.Run ? "runs a command" : null;
                string hk = HotkeyTable.Summary(E.GetHotkeys(), omen);
                if (hk != hotkeySubFor) { hotkeySubFor = hk; txtHotkeysSub.Text = hk; if (hotkeysOpen) BuildHotkeyPanel(); }
                tgEcoBattery.IsChecked = S.EcoOnBattery;
                tgSyncPower.IsChecked = S.SyncWinPower;
                tgAutostart.IsChecked = autostart;
                demoBadge.Visibility = E.Hw.IsDemo && screenshotPath == null ? Visibility.Visible : Visibility.Collapsed;
                bool err = (!E.BiosOk || E.ReadOnly) && !E.Hw.IsDemo;
                errBanner.Visibility = err ? Visibility.Visible : Visibility.Collapsed;
                if (err) txtErr.Text = !E.BiosOk ? "BIOS interface unavailable: " + E.LastError
                    : "Unsupported laptop (board " + E.Board + "). Read-only: nothing is written to the firmware. Run tools\\support-info.cmd and open a GitHub issue to add it.";
                bool firstHere = E.Generic && !E.Hw.IsDemo && !S.InfoDismissed && !Platforms.Reported(E.Board);
                infoBanner.Visibility = firstHere ? Visibility.Visible : Visibility.Collapsed;
                if (firstHere) txtInfo.Text = "You're the first to try this on your laptop. If it works, tell us on Discord or Reddit and we'll mark it verified.";
                RefreshTray();
                string tip = Program.DisplayName + " · " + E.ModeName + " · " + E.CurrentTdp + " W" + (S.Fan == FanMode.Max ? " · max fan" : "");
                tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
                UpdateFooter();
            } finally { syncing = wasSyncing; }
        }

        void UpdateFooter() {
            txtFoot.Foreground = Ui.Foot;
            if (E.Hw.IsDemo && screenshotPath == null) { dotHb.Fill = Ui.Brush(Ui.Warn); txtFoot.Text = "Demo · run as administrator"; return; }
            if (!E.BiosOk) { dotHb.Fill = Ui.Brush(Ui.Danger); txtFoot.Text = "Firmware unavailable"; return; }
            if (E.GuardActive) { dotHb.Fill = Ui.Brush(Ui.Danger); txtFoot.Foreground = Ui.Brush(Ui.Danger); txtFoot.Text = "Thermal guard · ambient " + E.GuardChassis + "°"; return; }
            double age = (DateTime.Now - E.LastHeartbeat).TotalSeconds;
            bool fresh = E.LastHeartbeat != DateTime.MinValue && age < E.S.HeartbeatSec * 2.5;
            dotHb.Fill = Ui.Brush(fresh ? Ui.Ok : Ui.Warn);

            txtFoot.Text = ShortModel() + (E.FanCount > 0 ? " · " + E.FanCount + " fans" : "");
            txtFoot.ToolTip = "Seal is driving this laptop's firmware" + (fresh ? ", last refreshed " + E.LastHeartbeat.ToString("HH:mm:ss") : "; the refresh is overdue");
        }

        bool sensorsSeen;
        void OnSensors(SensorSnapshot s) {
            E.CpuTemp = s.CpuTemp;
            E.CpuTempNow = s.CpuTempNow;
            E.GpuTemp = s.GpuTemp;
            E.CpuLoad = s.CpuLoad;
            E.GpuLoad = s.GpuLoad;
            E.SensorsSeen = true;
            if (overlayWin != null && overlayWin.IsVisible) overlayWin.UpdateVitals();
            if (!double.IsNaN(s.CpuTemp)) { int d = (int)Math.Round(s.CpuTemp); if (d != cpuTipFor) { cpuTipFor = d; chipCpu.ToolTip = "CPU is " + d + "\u00b0 right now"; } }
            onBattery = s.OnBattery;
            UpdateTrayTemp(s.CpuTemp);
            if (!double.IsNaN(s.CpuTemp)) { tempTrail.Add(s.CpuTemp); if (tempTrail.Count > 8) tempTrail.RemoveAt(0); sensorsSeen = true; }
            if (!IsVisible) return;
            if (cur == Page.Fans) { UpdateCurveLive(); UpdateMaxBlock(); UpdateFanFooter(); }
            bigCpu.Text = double.IsNaN(s.CpuTemp) ? "--" : s.CpuTemp.ToString("0", CultureInfo.InvariantCulture);
            bigGpu.Text = double.IsNaN(s.GpuTemp) ? "--" : s.GpuTemp.ToString("0", CultureInfo.InvariantCulture);
            bigCpu.Foreground = TempBrush(s.CpuTemp);
            bigGpu.Foreground = TempBrush(s.GpuTemp);
            runCpuHead.Text = "CPU" + (double.IsNaN(s.CpuLoad) ? "" : " · " + s.CpuLoad.ToString("0") + "%") + (double.IsNaN(s.CpuMhz) || s.CpuMhz <= 0 ? "" : " · " + (s.CpuMhz / 1000).ToString("0.0") + " GHz");
            runCpuWatts.Text = double.IsNaN(s.CpuWatts) ? "" : " · " + s.CpuWatts.ToString("0") + " W";


            string limits = PowerLimits(s);
            if (limits != cpuLimitsTip) { cpuLimitsTip = limits; runCpuWatts.ToolTip = limits; }
            runCpuTail.Text = s.Throttle.Length > 0 ? " · " + s.Throttle : "";


            subCpu.Foreground = s.Throttle.Length > 0 ? Ui.Brush(Ui.Warn) : subCpuBrush;
            if (s.CpuFromDriver != cpuTipFromDriver) {
                cpuTipFromDriver = s.CpuFromDriver;
                bigCpu.ToolTip = s.CpuFromDriver ? "Die temperature, read from the CPU itself" : null;
            }
            subGpu.Text = "GPU" + (double.IsNaN(s.GpuLoad) ? "" : " · " + s.GpuLoad.ToString("0") + "%") + (double.IsNaN(s.GpuWatts) ? "" : " · " + s.GpuWatts.ToString("0") + " W");
            txtFootRight.Text = s.BatteryPercent >= 0 && s.BatteryPercent <= 100 ? (s.OnBattery ? "Battery " : "AC · ") + s.BatteryPercent + "%" : "";
        }
        Brush TempBrush(double t) { return double.IsNaN(t) ? Ui.TextB : t >= E.GuardCpuHot ? Ui.Brush(Ui.Danger) : t >= E.P.Guard.WarnAt ? Ui.Brush(Ui.Warn) : Ui.TextB; }


        static string PowerLimits(SensorSnapshot s) {
            string a = double.IsNaN(s.Pl1) ? null : "PL1: " + s.Pl1.ToString("0", CultureInfo.InvariantCulture) + " W";
            string b = double.IsNaN(s.Pl2) ? null : "PL2: " + s.Pl2.ToString("0", CultureInfo.InvariantCulture) + " W";
            if (a == null && b == null) return null;
            return a != null && b != null ? a + " · " + b : (a ?? b);
        }

        void ReadHardwareAsync() {
            if (reading || (!E.BiosOk && !E.Hw.IsDemo)) return;
            reading = true;
            ThreadPool.QueueUserWorkItem(delegate {
                int[] f = null;
                int t = -1;
                try { f = E.Hw.GetFanLevels(); } catch (Exception ex) { Log.Write("read fans: " + ex.Message); }
                try { if (f != null) E.NoteFanLevels(f); } catch { }
                try { t = E.Hw.GetTemperature(); } catch { }
                Dispatcher.BeginInvoke((Action)delegate {
                    if (f != null) {
                        lastFans = f;
                        if (t >= 0) { lastChassis = t; if (t != chassisTipFor) { chassisTipFor = t; chipChassis.ToolTip = "Chassis is " + t + "\u00b0 right now"; } }
                        bigFan1.Text = Level(f[0]);
                        bigFan2.Text = Level(f[1]);
                        UpdateFanStatus();
                        if (cur == Page.Fans) UpdateMaxBlock();
                    }
                    if (t >= 0 && t != lastBiosTemp) {
                        lastBiosTemp = t;
                        GpuLevel g = E.EffectiveGpu;
                        txtHomeStatus.Text = (E.P.HasGpuPower ? (g == GpuLevel.Max ? "GPU max" : g == GpuLevel.Boost ? "GPU boost" : "GPU base") : E.P.HasPowerGain ? E.CurrentTdp + " W" : "")
                            + (ShowChassis(t) ? " · ambient " + t + "°" : "");
                    }
                    reading = false;
                });
            });
        }

        void OnKey(KeyAction a) {
            switch (a) {
                case KeyAction.Cycle: CycleWithFlash(); break;
                case KeyAction.Show: TogglePanel(); break;
                case KeyAction.MaxFan: ToggleMaxWithFlash(); break;
                case KeyAction.Run: RunKeyCommand(); break;
            }
        }
        void CycleWithFlash() {
            int next = (E.ModeIndex + 1) % 3;
            Flash(Engine.ModeNames[next] + " mode", ModeSubs[next], next);
            ApplyModeAsync(next);
        }
        void ToggleMaxWithFlash() {
            bool on = E.S.Fan != FanMode.Max;
            Flash(on ? "Max fan" : "Fans auto", on ? "Both fans to full speed" : "Back to the firmware curve", -1);
            Bg(delegate { E.ToggleMaxFan(); });
        }

        void OnPowerMode(object o, Microsoft.Win32.PowerModeChangedEventArgs e) {

            WinLighting.Forget();
            if (e.Mode == Microsoft.Win32.PowerModes.Resume) E.OnResume();
            else if (e.Mode == Microsoft.Win32.PowerModes.StatusChange) {
                bool bat = false;
                try { bat = WF.SystemInformation.PowerStatus.PowerLineStatus == WF.PowerLineStatus.Offline; } catch { }
                onBattery = bat;
                PollRate();
                Bg(delegate { E.OnPowerSource(bat); });
            }
        }

        bool ShowChassis(int c) { return c > 0 && E.P != null && E.P.Curve != null && E.P.Curve.UseChassis; }

        void PollRate() {

            bool curveDrivesFans = !E.ReadOnly && (E.S.Fan == FanMode.Auto || E.S.Fan == FanMode.Custom);

            sensors.SkipGpu = E.GpuMode == 3 || (onBattery && !IsVisible);
            int ms = IsVisible ? E.S.PollMs
                   : curveDrivesFans ? 5000
                   : E.S.TrayTemp ? (onBattery ? 10000 : 5000)
                   : (onBattery ? 30000 : 15000);
            sensors.SetInterval(ms);
            if (uiTimer == null) return;
            if (IsVisible) { if (!uiTimer.IsEnabled) uiTimer.Start(); } else uiTimer.Stop();
        }


        void OnSourceInit(object o, EventArgs e) {
            hwnd = new WindowInteropHelper(this).Handle;
            src = HwndSource.FromHwnd(hwnd);
            if (src != null) src.AddHook(Hook);
            try { int pref = 2; DwmSetWindowAttribute(hwnd, 33, ref pref, 4); int dark = 1; DwmSetWindowAttribute(hwnd, 20, ref dark, 4); int border = 0x0025282C; DwmSetWindowAttribute(hwnd, 34, ref border, 4); } catch { }
            if (E.S.Hotkeys) RegisterHotkeys();
            RegisterOverlayHotkey();
        }


        void RegisterHotkeys() {
            if (hotkeysRegistered) return;
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;
            Hotkey[] b = E.GetHotkeys();
            for (int i = 0; i < b.Length; i++) {
                hotkeyBusy[i] = false;
                if (b[i].IsEmpty) continue;


                if (!RegisterHotKey(h, i + 1, b[i].Mods | MOD_NOREPEAT, b[i].Vk)) { hotkeyBusy[i] = true; Log.Write("hotkey " + b[i] + " (" + HotkeyTable.Names[i] + ") not available: another program has it"); }
            }
            hotkeysRegistered = true;
            RegisterOverlayHotkey();
        }
        void UnregisterHotkeys() {
            if (!hotkeysRegistered) return;
            var h = new WindowInteropHelper(this).Handle;
            for (int i = 1; i <= HotkeyTable.Count; i++) UnregisterHotKey(h, i);
            UnregisterOverlayHotkey();
            hotkeysRegistered = false;
        }

        static System.Windows.Shapes.Path PencilGlyph(bool open) {
            const string pencil = "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1 1 0 0 0 0-1.41l-2.34-2.34a1 1 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z";
            const string tick = "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z";
            return new System.Windows.Shapes.Path { Data = Geometry.Parse(open ? tick : pencil), Fill = Ui.Sub, Width = 14, Height = 14, Stretch = Stretch.Uniform };
        }
        void ToggleHotkeyPanel() {
            hotkeysOpen = !hotkeysOpen;
            if (!hotkeysOpen) StopListening();

            btnHotkeys.Content = PencilGlyph(hotkeysOpen);
            btnHotkeys.ToolTip = hotkeysOpen ? "Done" : "Change the shortcuts: click one, then press the keys you want. Esc keeps the old one, Backspace removes it.";
            txtHotkeysSub.Visibility = hotkeysOpen ? Visibility.Collapsed : Visibility.Visible;
            if (hotkeysOpen) BuildHotkeyPanel();
            hotkeyPanel.Visibility = hotkeysOpen ? Visibility.Visible : Visibility.Collapsed;
            Remeasure(cur);
        }

        void BuildHotkeyPanel() {
            hotkeyPanel.Children.Clear();
            Hotkey[] b = E.GetHotkeys();
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int r = 0; r < b.Length; r++) g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            if (E.HotkeysCustomised) g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            for (int i = 0; i < b.Length; i++) {
                int idx = i;
                var name = new TextBlock { Text = HotkeyTable.Names[i], FontFamily = Ui.UiFont, FontSize = 13, Foreground = Ui.TextB, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
                Grid.SetRow(name, i);
                var cell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand, Background = Brushes.Transparent };
                Grid.SetRow(cell, i); Grid.SetColumn(cell, 1);
                if (listening == i) {
                    Border cap = KeyCap("Press keys…", accent);
                    listeningCap = (TextBlock)cap.Child;
                    cap.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(600)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
                    cell.Children.Add(cap);
                } else if (b[i].IsEmpty) {
                    cell.Children.Add(KeyCap("none", Ui.Desc));
                } else {
                    Border cap = KeyCap(b[i].ToString(), hotkeyBusy[i] ? Ui.Brush(Ui.Warn) : null);

                    if (hotkeyBusy[i]) { cap.BorderBrush = Ui.Brush(Ui.Warn); cap.ToolTip = "In use by another program, so it does nothing here. Click to pick a different one."; }
                    cell.Children.Add(cap);
                }
                name.MouseLeftButtonUp += delegate { StartListening(idx); };
                cell.MouseLeftButtonUp += delegate { StartListening(idx); };
                g.Children.Add(name);
                g.Children.Add(cell);
            }
            if (E.HotkeysCustomised) {
                var reset = new TextBlock { Text = "Reset all", FontFamily = Ui.UiFont, FontSize = 12.5, Foreground = accent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetRow(reset, b.Length); Grid.SetColumn(reset, 1);
                reset.MouseLeftButtonUp += delegate { StopListening(); UnregisterHotkeys(); E.ResetHotkeys(); if (HotkeysOn) RegisterHotkeys(); BuildHotkeyPanel(); };
                g.Children.Add(reset);
            }
            hotkeyPanel.Children.Add(g);
        }
        Border KeyCap(string text, Brush fg) {
            return new Border {
                Background = Ui.Pill, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0),
                BorderBrush = Ui.Line, BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = text, FontFamily = Ui.MonoFont, FontSize = 11, Foreground = fg ?? Ui.TextB }
            };
        }


        void StartListening(int idx) {
            listening = idx;
            UnregisterHotkeys();
            BuildHotkeyPanel();
            Focus();
        }


        bool HotkeysOn { get { return tgHotkeys.IsChecked == true; } }
        void StopListening() {
            if (listening < 0) return;
            listening = -1;
            if (HotkeysOn) RegisterHotkeys();
            if (hotkeysOpen) BuildHotkeyPanel();
        }
        void OnHotkeyCapture(object o, KeyEventArgs e) {
            if (listening < 0) return;
            e.Handled = true;
            int idx = listening;
            var action = (HotkeyAction)idx;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { StopListening(); return; }
            if (key == Key.Back) { E.SetHotkey(action, Hotkey.None); StopListening(); return; }
            Hotkey h;
            if (!Hotkey.FromKey(key, Keyboard.Modifiers, out h)) { ShowHeldModifiers(); return; }
            if (!h.Valid) { ShowToast("Hold Ctrl, Alt, Shift or Win with it, or use an F-key", true); return; }
            string[] before = E.SnapshotHotkeys();
            E.SetHotkey(action, h);
            listening = -1;
            hotkeyBusy[idx] = false;
            if (HotkeysOn) RegisterHotkeys();

            if (HotkeysOn && hotkeyBusy[idx]) {
                UnregisterHotkeys();
                E.RestoreHotkeys(before);
                if (HotkeysOn) RegisterHotkeys();
                ShowToast(h + " is already used by another program", true);
            }
            BuildHotkeyPanel();
        }


        void ShowHeldModifiers() {
            if (listening < 0 || listeningCap == null) return;
            uint mods = 0;
            ModifierKeys m = Keyboard.Modifiers;
            if ((m & ModifierKeys.Control) != 0) mods |= Hotkey.Ctrl;
            if ((m & ModifierKeys.Alt) != 0) mods |= Hotkey.Alt;
            if ((m & ModifierKeys.Shift) != 0) mods |= Hotkey.Shift;
            if ((m & ModifierKeys.Windows) != 0) mods |= Hotkey.Win;
            if (mods == 0) { listeningCap.Text = "Press keys\u2026"; return; }
            string t = new Hotkey(mods, 'X').ToString();
            listeningCap.Text = t.Substring(0, t.Length - 1) + "\u2026";
        }


        static string HoldText(int s) { return s % 60 == 0 && s >= 60 ? (s / 60) + " min" : s + " s"; }
        static TextBlock Word(string t) { return new TextBlock { Text = t, FontFamily = Ui.UiFont, FontSize = 12.5, Foreground = Ui.Desc, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }; }
        static string[] Degrees(int lo, int hi) { var r = new string[hi - lo + 1]; for (int i = 0; i < r.Length; i++) r[i] = (lo + i) + "\u00b0"; return r; }


        void BuildGuardLine() {
            guardLine.Children.Clear();
            int tj = 100;
            try { if (E.Cpu != null) { int t = E.Cpu.Poll(false).TjMax; if (t > 0) tj = t; } } catch { }

            cpuLo = Math.Min(70, E.GuardCpuHot); chassisLo = Math.Min(40, E.GuardChassisHot);
            chipCpu = new ValueLink { Items = Degrees(cpuLo, Math.Max(Math.Max(90, tj - 5), E.GuardCpuHot)) };
            chipChassis = new ValueLink { Items = Degrees(chassisLo, Math.Max(75, E.GuardChassisHot)) };

            int ceiling = E.P.Curve.Ceiling;
            var levels = new System.Collections.Generic.List<int> { 0 };
            for (int pct = 90; pct >= 50; pct -= 10) { int lvl = (int)Math.Round(ceiling * pct / 100.0); if (!levels.Contains(lvl)) levels.Add(lvl); }
            if (E.GuardLevel > 0 && !levels.Contains(E.GuardLevel)) { levels.Add(E.GuardLevel); levels.Sort(); levels.Reverse(); levels.Remove(0); levels.Insert(0, 0); }
            guardLevels = levels.ToArray();
            var names = new string[guardLevels.Length];
            for (int i = 0; i < names.Length; i++) names[i] = guardLevels[i] == 0 ? "max" : E.Rpm(guardLevels[i]);
            chipFans = new ValueLink { Items = names, ToolTip = "A stalled fan always gets max, whatever this says" };
            var holdList = new System.Collections.Generic.List<int>(HoldChoices);
            if (!holdList.Contains(E.GuardHoldSeconds)) { holdList.Add(E.GuardHoldSeconds); holdList.Sort(); }
            holdChoices = holdList.ToArray();
            var holds = new string[holdChoices.Length];
            for (int i = 0; i < holds.Length; i++) holds[i] = HoldText(holdChoices[i]);
            chipHold = new ValueLink { Items = holds, ToolTip = "How long it holds on after both readings are back under" };
            foreach (ValueLink v in new[] { chipCpu, chipChassis, chipFans, chipHold }) v.Editable = guardOpen;
            Action changed = delegate { if (syncing) return; guardDebounce.Stop(); guardDebounce.Start(); };
            chipCpu.Changed += delegate { changed(); };
            chipChassis.Changed += delegate { changed(); };
            chipFans.Changed += delegate { changed(); };
            chipHold.Changed += delegate { changed(); };

            foreach (string w in "When CPU".Split(' ')) guardLine.Children.Add(Word(w));
            guardLine.Children.Add(chipCpu);
            foreach (string w in "or chassis".Split(' ')) guardLine.Children.Add(Word(w));
            guardLine.Children.Add(chipChassis);
            foreach (string w in "turn fans".Split(' ')) guardLine.Children.Add(Word(w));
            guardLine.Children.Add(chipFans);
            guardLine.Children.Add(Word("for"));
            guardLine.Children.Add(chipHold);
        }

        void SyncGuardLine() {

            bool stale = E.GuardCpuHot < cpuLo || E.GuardCpuHot >= cpuLo + chipCpu.Items.Length
                || E.GuardChassisHot < chassisLo || E.GuardChassisHot >= chassisLo + chipChassis.Items.Length
                || Array.IndexOf(guardLevels, E.GuardLevel) < 0 || Array.IndexOf(holdChoices, E.GuardHoldSeconds) < 0;
            if (stale) BuildGuardLine();
            Synced(delegate {
                chipCpu.Index = E.GuardCpuHot - cpuLo;
                chipChassis.Index = E.GuardChassisHot - chassisLo;
                chipFans.Index = Array.IndexOf(guardLevels, E.GuardLevel);
                chipHold.Index = Array.IndexOf(holdChoices, E.GuardHoldSeconds);
            });
        }
        IntPtr Hook(IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled) {
            if (msg == WM_HOTKEY) {
                int wpInt = wp.ToInt32();
                if (wpInt == HOTKEY_OVERLAY) {
                    HandleOverlayHotkey();
                    handled = true;
                    return IntPtr.Zero;
                }
                int id = wpInt - 1;
                if (id >= 0 && id < HotkeyTable.Count && PassAltGr(id)) { handled = true; return IntPtr.Zero; }
                if (id >= 0 && id <= 2) { Flash(Engine.ModeNames[id] + " mode", ModeSubs[id], id); ApplyModeAsync(id); }
                else if (id == (int)HotkeyAction.MaxFan) ToggleMaxWithFlash();
                else if (id == (int)HotkeyAction.Cycle) CycleWithFlash();
                handled = true;
            }
            return IntPtr.Zero;
        }

        bool PassAltGr(int id) {
            if ((GetAsyncKeyState(0xA5) & 0x8000) == 0) return false;
            Hotkey k = E.GetHotkey((HotkeyAction)id);
            IntPtr layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
            if (!k.TypesCharacter(layout)) return false;
            IntPtr h = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(h, id + 1);
            byte scan = (byte)MapVirtualKey(k.Vk, 0);
            keybd_event((byte)k.Vk, scan, 0, UIntPtr.Zero);
            keybd_event((byte)k.Vk, scan, 2, UIntPtr.Zero);
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            t.Tick += delegate {
                t.Stop();

                if (hotkeysRegistered && !exiting && E.GetHotkey((HotkeyAction)id).Same(k)) RegisterHotKey(h, id + 1, k.Mods | MOD_NOREPEAT, k.Vk);
            };
            t.Start();
            return true;
        }

        void OnLoaded(object o, RoutedEventArgs e) {
            string page = Program.StartPage;
            if (openSettings) page = "settings";
            if (Program.KeyboardTest) page = "keyboard";
            if (page == "fans") Navigate(Page.Fans, false);
            else if (page == "memory") Navigate(Page.Memory, false);
            else if (page == "overlay") Navigate(Page.Overlay, false);
            else if (page == "keyboard" && E.Light != null) Navigate(Page.Keyboard, false);
            else if (page == "settings") Navigate(Page.Settings, false);
            else if (page == "update") ShowUpdateRow();
            else if (page == "driver") ShowDriverRow();
            else if (page == "hotkeys") { Navigate(Page.Settings, false); ToggleHotkeyPanel(); }
            else if (page == "guard") { Navigate(Page.Settings, false); btnGuard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            else if (page == "guardrow") Navigate(Page.Settings, false);
            Morph(false);
            if (Program.JustUpdated) ShowToast("Updated to " + Program.Version, false);
            if (Program.FlashTest) Flash("Performance mode", ModeSubs[2], 2);
            if (screenshotPath == null) return;
            var started = DateTime.Now;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Program.FlashTest ? 600 : 500) };
            t.Tick += delegate {
                double age = (DateTime.Now - started).TotalMilliseconds;
                if (!Program.FlashTest && !sensorsSeen && age < 9000) return;
                if (!Program.FlashTest && age < 2800) return;
                t.Stop();
                try {
                    Morph(false);
                    root.UpdateLayout();


                    FrameworkElement at = Program.StartPage == "gfx" || Program.StartPage == "display" ? gfxRow : Program.StartPage == "driver" ? driverRow : Program.StartPage == "update" ? updateRow : Program.StartPage == "hotkeys" ? (FrameworkElement)hotkeyPanel : Program.StartPage == "guard" || Program.StartPage == "guardrow" ? (FrameworkElement)txtGuardSub.Parent : null;
                    if (at != null && cur == Page.Settings) {
                        try {
                            var content = scroll.Content as FrameworkElement;
                            if (content != null) { scroll.ScrollToVerticalOffset(Math.Max(0, at.TranslatePoint(new Point(0, 0), content).Y - 96)); root.UpdateLayout(); }
                        } catch { }
                    }
                    SnapshotElement(root, screenshotPath);
                    Log.Write("screenshot saved " + screenshotPath);
                    if (osd != null && osd.IsVisible) { var f = (FrameworkElement)osd.Content; osd.Opacity = 1; SnapshotElement(f, screenshotPath + ".osd.png"); }
                    if (overlayWin != null && overlayWin.IsVisible) { var fo = (FrameworkElement)overlayWin.Content; SnapshotElement(fo, screenshotPath + ".overlay.png"); }
                    if (overlayPerfWin != null && overlayPerfWin.IsVisible) { var fp = (FrameworkElement)overlayPerfWin.Content; SnapshotElement(fp, screenshotPath + ".perf.png"); }
                    try { using (var bmp = DrawMark(SD.Color.FromArgb(Ui.BalColor.R, Ui.BalColor.G, Ui.BalColor.B), 256)) bmp.Save(screenshotPath + ".icon.png", System.Drawing.Imaging.ImageFormat.Png); } catch { }
                    try { using (var bmp = DrawMark(SD.Color.FromArgb(Ui.PerfColor.R, Ui.PerfColor.G, Ui.PerfColor.B), 32, "70")) bmp.Save(screenshotPath + ".tray.png", System.Drawing.Imaging.ImageFormat.Png); } catch { }
                } catch (Exception ex) { Log.Write("screenshot failed: " + ex); }
                ExitApp();
            };
            t.Start();
        }
        static void SnapshotElement(FrameworkElement el, string path) {
            el.UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(el);
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(el.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(el.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            rtb.Render(el);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(path)) enc.Save(fs);
        }

        void Position() {
            var S = E.S;
            if (screenshotPath != null) { WindowStartupLocation = WindowStartupLocation.Manual; Left = -4000; Top = 0; ShowInTaskbar = false; return; }
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            if (S.WinX != -1 && S.WinY != -1 && S.WinX >= vl && S.WinY >= vt && S.WinX < vl + SystemParameters.VirtualScreenWidth - 100 && S.WinY < vt + SystemParameters.VirtualScreenHeight - 100) {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = S.WinX;
                Top = S.WinY;
            } else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        void EnsureMainWindowOnScreen() {
            var wa = SystemParameters.WorkArea;
            if (double.IsNaN(Left) || Left < wa.Left - 50 || Left > wa.Right - 100 ||
                double.IsNaN(Top) || Top < wa.Top - 50 || Top > wa.Bottom - 100) {
                Left = Math.Max(wa.Left + 20, wa.Left + (wa.Width - (ActualWidth > 100 ? ActualWidth : 760)) / 2);
                Top = Math.Max(wa.Top + 20, wa.Top + (wa.Height - (ActualHeight > 100 ? ActualHeight : 520)) / 2);
            }
        }

        public void ForceToForeground() {
            try {
                var h = new WindowInteropHelper(this).Handle;
                if (h == IntPtr.Zero) return;
                ShowWindow(h, 9);
                IntPtr foreHwnd = GetForegroundWindow();
                uint foreThread = GetWindowThreadProcessId(foreHwnd, IntPtr.Zero);
                uint appThread = GetCurrentThreadId();
                if (foreThread != 0 && foreThread != appThread) {
                    AttachThreadInput(appThread, foreThread, true);
                    BringWindowToTop(h);
                    SetForegroundWindow(h);
                    SwitchToThisWindow(h, true);
                    AttachThreadInput(appThread, foreThread, false);
                } else {
                    BringWindowToTop(h);
                    SetForegroundWindow(h);
                    SwitchToThisWindow(h, true);
                }
                SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
                SetWindowPos(h, HWND_NOTOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
            } catch { }
        }

        public void ShowPanel() {
            EnsureMainWindowOnScreen();
            Show();
            WindowState = WindowState.Normal;
            Activate();
            ForceToForeground();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate { Morph(false); PlaceRailPill(false); });
        }
        void TogglePanel() { if (IsVisible) HideToTray(); else ShowPanel(); }
        void HideToTray() {
            StopListening();
            StopMorph();
            E.S.Save();
            Hide();


            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate {
                if (IsVisible) return;
                try { GC.Collect(2, GCCollectionMode.Optimized); SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1)); } catch { }
            });
        }
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr h, IntPtr min, IntPtr max);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        void StartShowListener() {
            var t = new Thread(delegate() {
                var handles = new List<WaitHandle>();
                if (Program.ShowEvent != null) handles.Add(Program.ShowEvent);
                if (Program.ExitEvent != null) handles.Add(Program.ExitEvent);
                if (handles.Count == 0) return;
                while (!exiting) {
                    try {
                        int i = WaitHandle.WaitAny(handles.ToArray(), 1000);
                        if (i == WaitHandle.WaitTimeout) continue;
                        if (handles[i] == Program.ExitEvent) { Log.Write("exit requested by another instance"); Dispatcher.BeginInvoke((Action)ExitApp); break; }
                        Dispatcher.BeginInvoke((Action)ShowPanel);
                    } catch { break; }
                }
            }) { IsBackground = true, Name = "show-listener" };
            t.Start();
        }
        bool resetting, quietExit;
        void ExitApp() {
            if (exiting) return;
            exiting = true;
            if (resetting) { try { E.S.Delete(); } catch { } } else { try { E.S.Save(); } catch { } }
            try { UnregisterHotkeys(); } catch { }
            try { uiTimer.Stop(); } catch { }
            try { Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerMode; } catch { }
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); } } catch { }
            try { if (osd != null) osd.Close(); } catch { }
            try { if (overlayWin != null) overlayWin.Close(); } catch { }
            try { if (overlayPerfWin != null) overlayPerfWin.Close(); } catch { }
            try { if (trayTempIcon != null) { IntPtr h = trayTempIcon.Handle; trayTempIcon.Dispose(); DestroyIcon(h); } } catch { }
            try { E.Park(quietExit); } catch { }

            try { sensors.Dispose(); } catch { }
            try { E.Dispose(); } catch { }
            Log.Write("exit");

            if (resetting) { try { Log.Delete(); } catch { } }
            Application.Current.Shutdown();
        }


        void ShowToast(string msg, bool err) {
            if (E.GuardActive && !err) return;
            txtToast.Text = msg;
            txtToast.Foreground = err ? Ui.Brush(Ui.Danger) : Ui.TextB;
            Ui.Fade(toast, 0.92, 140);
            if (toastTimer == null) {
                toastTimer = new DispatcherTimer();
                toastTimer.Tick += delegate { toastTimer.Stop(); Ui.Fade(toast, 0, 260); };
            }
            toastTimer.Interval = TimeSpan.FromMilliseconds(err ? 4200 : 1900);
            toastTimer.Stop();
            toastTimer.Start();
        }


        void QueryAutostartAsync() {
            Slow(delegate {
                bool on = RunSchtasks("/Query /TN " + Program.AppName) == 0;
                if (on && !E.Hw.IsDemo) {

                    string xml = SchtasksOut("/Query /TN " + Program.AppName + " /XML");
                    if (xml.IndexOf("<StopIfGoingOnBatteries>true", StringComparison.OrdinalIgnoreCase) >= 0 || xml.IndexOf("<DisallowStartIfOnBatteries>true", StringComparison.OrdinalIgnoreCase) >= 0) {
                        Log.Write("logon task has battery restrictions; re-registering it");
                        SetAutostart(true);
                        on = autostart;
                    }
                }
                if (!on && E.S.FirstRun && !E.Hw.IsDemo) {
                    SetAutostart(true);
                    on = autostart;
                    if (on) Dispatcher.BeginInvoke((Action)delegate { ShowToast("Starts with Windows from now on (Settings to change)", false); });
                }
                Dispatcher.BeginInvoke((Action)delegate { autostart = on; syncing = true; tgAutostart.IsChecked = on; syncing = false; });
            });
        }
        void SetAutostart(bool on) {
            if (E.Hw.IsDemo) { Dispatcher.BeginInvoke((Action)delegate { ShowToast("Autostart needs the administrator build", true); }); return; }
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            int rc;
            if (on) {

                string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Program.FileStem + "-task.xml");
                try { System.IO.File.WriteAllText(tmp, TaskXml(exe), System.Text.Encoding.Unicode); } catch (Exception ex) { Log.Write("task xml: " + ex.Message); }
                rc = RunSchtasks("/Create /TN " + Program.AppName + " /XML \"" + tmp + "\" /F");
                try { System.IO.File.Delete(tmp); } catch { }
            } else rc = RunSchtasks("/Delete /TN " + Program.AppName + " /F");
            Log.Write("autostart " + on + " rc=" + rc);
            autostart = on && rc == 0;
            if (!E.S.FirstRun) Dispatcher.BeginInvoke((Action)delegate { ShowToast(rc == 0 ? (on ? "Starts with Windows" : "Autostart removed") : "schtasks failed (" + rc + ")", rc != 0); });
        }

        static string TaskXml(string exe) {
            string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n" +
                "<Task version=\"1.4\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\n" +
                "  <RegistrationInfo><Description>" + Program.AppName + " starts with Windows</Description></RegistrationInfo>\n" +
                "  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + sid + "</UserId></LogonTrigger></Triggers>\n" +
                "  <Principals><Principal id=\"Author\"><UserId>" + sid + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\n" +
                "  <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>false</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable>" +
                "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
                "<AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun>" +
                "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority></Settings>\n" +
                "  <Actions Context=\"Author\"><Exec><Command>" + System.Security.SecurityElement.Escape(exe) + "</Command><Arguments>--hidden</Arguments></Exec></Actions>\n" +
                "</Task>\n";
        }
        static string SchtasksOut(string args) {
            try {
                var psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                var outLines = new System.Text.StringBuilder();
                using (var p = new Process()) {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (outLines) outLines.Append(e.Data).Append("\n"); };
                    p.Start();
                    p.BeginOutputReadLine();
                    if (p.WaitForExit(5000)) { p.WaitForExit(); return outLines.ToString(); }
                    try { p.Kill(); } catch { }
                    p.WaitForExit(2000);
                    if (!p.HasExited) Log.Write("could not stop schtasks: " + args);
                    Log.Write("schtasks timed out: " + args);
                    return "";
                }
            } catch (Exception ex) { Log.Write("schtasks: " + ex.Message); return ""; }
        }
        static int RunSchtasks(string args) {
            try {
                using (var p = Process.Start(new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden })) {
                    if (p.WaitForExit(5000)) return p.ExitCode;
                    try { p.Kill(); } catch { }
                    p.WaitForExit(2000);
                    if (!p.HasExited) Log.Write("could not stop schtasks: " + args);
                    Log.Write("schtasks timed out: " + args);
                    return -1;
                }
            } catch (Exception ex) { Log.Write("schtasks: " + ex.Message); return -1; }
        }
    }
}
