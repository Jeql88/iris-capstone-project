using System.Runtime.InteropServices;

namespace IRIS.Agent
{
    /// <summary>
    /// Consolidated Win32 P/Invoke declarations for the IRIS Agent.
    /// </summary>
    internal static class NativeMethods
    {
        // --- Cursor / Icon (used by ScreenSnapshotServer) ---

        [DllImport("user32.dll")]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        public static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        public const int CURSOR_SHOWING = 0x00000001;

        // --- Wallpaper (used by WallpaperPolicyEnforcer) ---

        public const int SPI_SETDESKWALLPAPER = 20;
        public const int SPIF_UPDATEINIFILE = 0x01;
        public const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        // --- Console QuickEdit fix (used by Program) ---

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        public const int STD_INPUT_HANDLE = -10;
        public const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        public const uint ENABLE_EXTENDED_FLAGS = 0x0080;

        // --- Idle detection (used by AgentWorker) ---

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        // --- Console window visibility (used by Program for --background mode) ---

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_HIDE = 0;

        // --- DPI awareness (used by Program at startup so screen capture sees
        //     physical pixels on high-DPI monitors; otherwise Screen.Bounds is
        //     virtualised while CopyFromScreen reads physical pixels, producing
        //     a top-left-only snapshot). Requires Windows 10 1703+. ---

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiAwarenessContext);

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

        // --- PrintWindow-based screen capture fallback (used on Windows 11 24H2
        //     where Graphics.CopyFromScreen fails with ERROR_INVALID_HANDLE).
        //     PrintWindow routes through DWM so it survives the 24H2 GDI
        //     session-DC regression. ---

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        // PW_RENDERFULLCONTENT asks DWM for composited output (incl. UWP/hardware-
        // accelerated surfaces). Required for modern Windows; ignored pre-8.1.
        public const uint PW_RENDERFULLCONTENT = 0x00000002;

        // --- Primary-monitor lookup and D3D11 interop for Windows.Graphics.Capture.
        //     Used by GraphicsCaptureLogic (final fallback path on Win11 24H2). ---

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

        [DllImport("d3d11.dll", ExactSpelling = true, SetLastError = false)]
        public static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int DriverType,
            IntPtr Software,
            uint Flags,
            IntPtr pFeatureLevels,
            uint FeatureLevels,
            uint SDKVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        public const int D3D_DRIVER_TYPE_HARDWARE = 1;
        public const int D3D_DRIVER_TYPE_WARP = 5;
        public const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        public const uint D3D11_SDK_VERSION = 7;

        [DllImport("d3d11.dll", ExactSpelling = true, SetLastError = false)]
        public static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);
    }
}
