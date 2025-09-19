using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FanShop.Utils;
using FanShop.ViewModels;
using System.Windows.Media.Animation;

namespace FanShop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        
        animation.Completed += (s, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }
    
    public MainWindow()
    {
        InitializeComponent();
        
        DataContext = this;
    }
    
    protected override async void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (DataContext is MainWindowViewModel vm)
        {
            await vm.CheckAndUpdateCalendarAsync();
        }
    }
    
    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;

        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

        if (monitor != IntPtr.Zero)
        {
            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;
            var monitorArea = monitorInfo.rcMonitor;

            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            mmi.ptMaxPosition.x = workArea.Left - monitorArea.Left;
            mmi.ptMaxPosition.y = workArea.Top - monitorArea.Top;
            mmi.ptMaxSize.x = workArea.Right - workArea.Left;
            mmi.ptMaxSize.y = workArea.Bottom - workArea.Top;

            Marshal.StructureToPtr(mmi, lParam, true);
        }
    }
    
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }
    
    private void ClickableArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!OpenWindowsController.IsMainWindow())
        {
            e.Handled = true;
            return;
        }
    }
}