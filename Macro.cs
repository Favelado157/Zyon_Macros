using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GhosttzzKBot;

record MacroEvent(long Time, int Message, int X, int Y, int Data);
record Recording(long Duration, List<MacroEvent> Events);

sealed class Macro : IDisposable
{
    delegate nint Hook(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)]
    struct Mouse { public int X, Y, Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct Keyboard { public int Key, Scan, Flags, Time; public nuint Extra; }
    [DllImport("user32", SetLastError=true)] static extern nint SetWindowsHookEx(int id, Hook hook, nint module, int thread);
    [DllImport("user32")] static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32")] static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32", CharSet=CharSet.Unicode)] static extern nint GetModuleHandle(string? name);
    [DllImport("user32")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32")] static extern void mouse_event(uint flags, int x, int y, int data, nuint extra);
    [DllImport("user32")] static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);

    readonly Hook mouseHook, keyboardHook;
    readonly Stopwatch clock = new();
    nint mouseHandle, keyboardHandle;
    public bool Active { get; private set; }
    public bool Paused => Active && !clock.IsRunning;
    public long Duration => Active ? clock.ElapsedMilliseconds : Saved.Duration;
    public Recording Saved { get; set; } = new(0, []);
    public Settings Options { get; set; }
    public Func<Point, bool>? IsPanelPoint { get; set; }
    bool ignoreLeftRelease;
    public Macro(Settings options)
    {
        Options = options;
        mouseHook = OnMouse;
        keyboardHook = OnKeyboard;
    }

    public void Start()
    {
        var module = GetModuleHandle(null);
        mouseHandle = SetWindowsHookEx(14, mouseHook, module, 0);
        keyboardHandle = SetWindowsHookEx(13, keyboardHook, module, 0);
        if (mouseHandle == 0 || keyboardHandle == 0)
        {
            Dispose();
            throw new InvalidOperationException("O Windows não conseguiu iniciar a captura de entrada.");
        }
        Saved = new(0, []);
        Active = true;
        ignoreLeftRelease = false;
        clock.Restart();
    }
    public void Pause()
    {
        if (Paused) clock.Start();
        else clock.Stop();
    }
    public void Finish()
    {
        clock.Stop();
        Saved = Saved with { Duration = Math.Min(86_400_000, clock.ElapsedMilliseconds) };
        Dispose();
    }
    void Add(int message, int x, int y, int data)
    {
        if (Saved.Events.Count >= 250_000 || clock.ElapsedMilliseconds > 86_400_000) { clock.Stop(); return; }
        Saved.Events.Add(new(clock.ElapsedMilliseconds, message, x, y, data));
    }
    nint OnMouse(int code, nint message, nint data)
    {
        if (code >= 0 && clock.IsRunning)
        {
            var e = Marshal.PtrToStructure<Mouse>(data);
            int m = (int)message;
            bool enabled = m switch { 512 => Options.Move, 522 or 526 => Options.Wheel && Options.Mode == 0, _ => Options.Clicks && Options.Mode == 0 };
            if ((e.Flags & 1) == 0 && enabled)
            {
                bool panelClick = m is 513 or 514 && IsPanelPoint?.Invoke(new(e.X,e.Y)) == true;
                if (m == 513) ignoreLeftRelease = panelClick;
                if (panelClick || (m == 514 && ignoreLeftRelease))
                {
                    if (m == 514) ignoreLeftRelease = false;
                }
                else if (m != 512 || Saved.Events.Count == 0 || clock.ElapsedMilliseconds - Saved.Events[^1].Time >= Options.Sample) Add(m, e.X, e.Y, e.Data >> 16);
            }
        }
        return CallNextHookEx(0, code, message, data);
    }
    nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0 && clock.IsRunning && Options.Keyboard && Options.Mode == 0)
        {
            var e = Marshal.PtrToStructure<Keyboard>(data);
            if ((e.Flags & 16) == 0 && !Options.Keys.Contains(e.Key)) Add((int)message, e.Key, e.Scan, e.Flags & 1);
        }
        return CallNextHookEx(0, code, message, data);
    }
    public static async Task Replay(Recording recording, Settings options, CancellationToken cancellation)
    {
        var heldKeys = new Dictionary<int, MacroEvent>();
        var heldButtons = new Dictionary<int, MacroEvent>();
        try
        {
            do
            {
                var timer = Stopwatch.StartNew();
                int processed = 0;
                foreach (var e in recording.Events)
                {
                    if (++processed % 64 == 0) await Task.Yield();
                    int delay = (int)(e.Time / options.Speed - timer.ElapsedMilliseconds);
                    if (delay > 0) await Task.Delay(delay, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if (e.Message < 512 && options.Keys.Contains(e.X)) continue;
                    if (e.Message is 256 or 260) { SendKey(e, false); heldKeys[e.X] = e; }
                    else if (e.Message is 257 or 261) { SendKey(e, true); heldKeys.Remove(e.X); }
                    else
                    {
                        SetCursorPos(e.X, e.Y);
                        uint flag = e.Message switch { 513 => 2, 514 => 4, 516 => 8, 517 => 16, 519 => 32, 520 => 64, 523 => 128, 524 => 256, 522 => 2048, 526 => 4096, _ => 0 };
                        if (flag != 0) mouse_event(flag, 0, 0, e.Data, 0);
                        int button = e.Message >= 523 ? e.Data + 523 : e.Message - (e.Message is 514 or 517 or 520 ? 1 : 0);
                        if (e.Message is 513 or 516 or 519 or 523) heldButtons[button] = e;
                        if (e.Message is 514 or 517 or 520 or 524) heldButtons.Remove(button);
                    }
                }
                var tail = (int)(recording.Duration / options.Speed - timer.ElapsedMilliseconds);
                if (tail > 0) await Task.Delay(tail, cancellation);
                Release();
                if (options.Repeat) await Task.Delay(Math.Max(1, options.Interval), cancellation);
            } while (options.Repeat);
        }
        finally { Release(); }
        void Release()
        {
            foreach (var e in heldKeys.Values) SendKey(e, true);
            foreach (var e in heldButtons.Values)
                mouse_event(e.Message switch { 513 => 4u, 516 => 16u, 519 => 64u, _ => 256u }, 0, 0, e.Data, 0);
            heldKeys.Clear();
            heldButtons.Clear();
        }
    }
    static void SendKey(MacroEvent e, bool up) => keybd_event((byte)e.X, (byte)e.Y, (uint)e.Data | (up ? 2u : 0u), 0);
    public void Dispose()
    {
        clock.Stop();
        Active = false;
        if (mouseHandle != 0) UnhookWindowsHookEx(mouseHandle);
        if (keyboardHandle != 0) UnhookWindowsHookEx(keyboardHandle);
        mouseHandle = keyboardHandle = 0;
    }
}
