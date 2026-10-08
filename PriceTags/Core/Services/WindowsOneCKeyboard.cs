using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace FanShop.PriceTags.Services;

public sealed class WindowsOneCKeyboard : IOneCKeyboard
{
    private nint _target;
    private uint _processId;
    public bool IsSupported => OperatingSystem.IsWindows();
    public void CaptureTarget()
    {
        if (!IsSupported) throw new PlatformNotSupportedException("Автоматический ввод доступен только в Windows.");
        var target = GetForegroundWindow();
        GetWindowThreadProcessId(target, out var pid);
        using var process = Process.GetProcessById((int)pid);
        if (!new[] { "1cv8", "1cv8c" }.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Активное окно не принадлежит 1С (1cv8/1cv8c). Поставьте курсор в «Номенклатура» и нажмите F8 в окне 1С.");
        _target = target; _processId = pid; ValidateTarget();
    }
    public void ValidateTarget()
    {
        if (_target == 0 || GetForegroundWindow() != _target)
            throw new InvalidOperationException("Активное окно сменилось. Автоматический ввод остановлен.");
        GetWindowThreadProcessId(_target, out var pid);
        if (pid != _processId) throw new InvalidOperationException("Окно 1С закрылось или изменилось.");
        if (new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
            throw new InvalidOperationException("Зажата Shift/Ctrl/Alt/Win. Отпустите клавиши и проверьте строку 1С.");
    }
    public void TypeText(string text, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Any(char.IsControl)) throw new InvalidDataException("Значение для ввода пустое или содержит управляющие символы.");
        foreach (var character in text)
        {
            token.ThrowIfCancellationRequested(); ValidateTarget();
            SendPair(0, character, 0x0004);
            if (token.WaitHandle.WaitOne(8)) token.ThrowIfCancellationRequested();
        }
    }
    public void Enter(CancellationToken token) { token.ThrowIfCancellationRequested(); ValidateTarget(); SendPair(0x0D, 0, 0); }
    private static void SendPair(ushort vk, ushort scan, uint flags)
    {
        var inputs = new[] { Input.Key(vk, scan, flags), Input.Key(vk, scan, flags | 0x0002) };
        if (SendInput(2, inputs, Marshal.SizeOf<Input>()) != 2)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows заблокировала ввод. Запустите FanShop и 1С с одинаковыми правами и проверьте текущую строку.");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input
    {
        public uint Type; public InputUnion Data;
        public static Input Key(ushort vk, ushort scan, uint flags) => new() { Type = 1, Data = new() { Keyboard = new() { VirtualKey = vk, Scan = scan, Flags = flags } } };
    }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}

// Dedicated native message loop keeps emergency keys independent of Avalonia's UI thread.
public sealed class GlobalTransferHotkeys : IDisposable
{
    private Thread? _thread;
    private uint _threadId;
    public event Action? StartRequested;
    public event Action? PauseRequested;
    public event Action? StopRequested;
    public bool Armed => _thread is not null;
    public async Task ArmAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Глобальные клавиши доступны только в Windows.");
        if (_thread is not null) return;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() =>
        {
            var registered = new List<int>();
            try
            {
                _threadId = GetCurrentThreadId();
                PeekMessage(out _, 0, 0, 0, 0); // create thread message queue
                foreach (var pair in new[] { (1, 0x77u), (2, 0x78u), (3, 0x1Bu) })
                {
                    if (!RegisterHotKey(0, pair.Item1, 0x4000, pair.Item2))
                        throw new InvalidOperationException("Не удалось зарегистрировать F8/F9/Esc. Клавиши заняты другой программой. Передача запрещена до освобождения клавиш.");
                    registered.Add(pair.Item1);
                }
                ready.TrySetResult();
                while (GetMessage(out var message, 0, 0, 0) > 0)
                {
                    if (message.Message != 0x0312) continue;
                    switch ((int)message.WParam)
                    {
                        case 1: StartRequested?.Invoke(); break;
                        case 2: PauseRequested?.Invoke(); break;
                        case 3: StopRequested?.Invoke(); break;
                    }
                }
            }
            catch (Exception ex) { ready.TrySetException(ex); }
            finally { foreach (var id in registered) UnregisterHotKey(0, id); }
        }) { IsBackground = true, Name = "FanShop 1C hotkeys" };
        _thread.Start();
        try { await ready.Task; }
        catch { _thread.Join(); _thread = null; throw; }
    }
    public void Disarm()
    {
        var thread = _thread; if (thread is null) return;
        PostThreadMessage(_threadId, 0x0012, 0, 0);
        if (Thread.CurrentThread != thread) thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }
    public void Dispose() => Disarm();
    [StructLayout(LayoutKind.Sequential)] private struct MessageData { public nint HWnd; public uint Message; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out MessageData message, nint window, uint min, uint max);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out MessageData message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostThreadMessage(uint threadId, uint message, nuint wparam, nint lparam);
}
