using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using AutomationToolkit.Core.Interop;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Hooks;

/// <summary>WH_KEYBOARD_LL / WH_MOUSE_LL をアプリ生存中インストールし続けるグローバル入力フック</summary>
/// <remarks>
/// 録画・ホットキー検出・注入入力フィルタの共通基盤。
/// フックコールバックは専用スレッドで実行され、遅いフックは Windows に自動除去されるため
/// 構造体コピーと Channel 書き込みしかしない。購読者への配信は別の消費タスクで行う
/// </remarks>
public sealed class LowLevelInputHook : IDisposable
{
    /// <summary>フックスレッドから消費タスクへイベントを受け渡すチャネル</summary>
    private readonly Channel<RawInputEvent> _channel = Channel.CreateUnbounded<RawInputEvent>(
        new UnboundedChannelOptions { SingleReader = true });

    /// <summary>フックをインストールしてメッセージポンプを回す専用スレッド</summary>
    private Thread? _hookThread;

    /// <summary>購読者へイベントを配信する消費タスク</summary>
    private Task? _dispatchTask;

    /// <summary>フックスレッドのスレッド ID</summary>
    private uint _hookThreadId;

    /// <summary>キーボードフックのハンドル</summary>
    private nint _keyboardHook;

    /// <summary>マウスフックのハンドル</summary>
    private nint _mouseHook;

    /// <summary>キーボードフックのコールバック。GC に回収されないようフィールドに保持する</summary>
    private HookProc? _keyboardProc;

    /// <summary>マウスフックのコールバック。GC に回収されないようフィールドに保持する</summary>
    private HookProc? _mouseProc;

    /// <summary>すでに Dispose 済みかどうか</summary>
    private volatile bool _disposed;

    /// <summary>入力イベント。専用の消費タスク上で順序どおりに 1 件ずつ発火する</summary>
    public event Action<RawInputEvent>? InputReceived;

    /// <summary>キーイベントに対する同期フィルタ。ホットキー検出に使用する</summary>
    /// <remarks>
    /// フックスレッド上で呼ばれるため必ず高速に返すこと。
    /// true を返すとそのイベントは飲み込まれ、フォアグラウンドアプリにも購読者にも届かない
    /// </remarks>
    public Func<RawInputEvent, bool>? KeyEventFilter { get; set; }

    /// <summary>フック専用スレッドを起動してフックをインストールし、配信タスクを開始する</summary>
    /// <exception cref="InvalidOperationException">すでに開始済みの場合</exception>
    /// <exception cref="Win32Exception">フックのインストールに失敗した場合</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hookThread is not null)
        {
            throw new InvalidOperationException("フックは既に開始されています。");
        }

        var ready = new ManualResetEventSlim();
        Exception? startupError = null;

        _hookThread = new Thread(() =>
        {
            try
            {
                _hookThreadId = NativeMethods.GetCurrentThreadId();
                _keyboardProc = KeyboardHookProc;
                _mouseProc = MouseHookProc;
                var hModule = NativeMethods.GetModuleHandle(null);
                _keyboardHook = NativeMethods.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, hModule, 0);
                _mouseHook = NativeMethods.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, hModule, 0);
                if (_keyboardHook == 0 || _mouseHook == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "入力フックのインストールに失敗しました。");
                }
            }
            catch (Exception ex)
            {
                startupError = ex;
                return;
            }
            finally
            {
                ready.Set();
            }

            // LL フックはインストールしたスレッドにメッセージポンプが必要
            while (NativeMethods.GetMessage(out var msg, 0, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
        })
        {
            IsBackground = true,
            Name = "AutomationToolkit.InputHook",
        };
        _hookThread.Start();
        ready.Wait();
        if (startupError is not null)
        {
            _hookThread = null;
            throw startupError;
        }

        _dispatchTask = Task.Run(DispatchLoopAsync);
    }

    /// <summary>チャネルからイベントを読み取り、購読者へ順番に配信する</summary>
    /// <returns>配信ループの完了を表すタスク</returns>
    private async Task DispatchLoopAsync()
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                InputReceived?.Invoke(evt);
            }
            catch
            {
                // 購読者の例外で配信ループを止めない
            }
        }
    }

    /// <summary>キーボードフックのコールバック</summary>
    /// <param name="nCode">フックコード</param>
    /// <param name="wParam">ウィンドウメッセージ種別</param>
    /// <param name="lParam">KBDLLHOOKSTRUCT へのポインタ</param>
    /// <returns>イベントを飲み込む場合は 1、それ以外は次のフックの戻り値</returns>
    private nint KeyboardHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var msg = (uint)wParam;
            var kind = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN
                ? RawInputKind.KeyDown
                : RawInputKind.KeyUp;
            var evt = new RawInputEvent(
                kind,
                (ushort)data.vkCode,
                (ushort)data.scanCode,
                (data.flags & Win32.LLKHF_EXTENDED) != 0,
                default,
                0, 0, 0, false,
                (data.flags & Win32.LLKHF_INJECTED) != 0,
                Stopwatch.GetTimestamp());

            // ホットキーとして飲み込む場合は購読者にも届けない
            if (KeyEventFilter?.Invoke(evt) == true)
            {
                return 1;
            }
            _channel.Writer.TryWrite(evt);
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    /// <summary>マウスフックのコールバック</summary>
    /// <param name="nCode">フックコード</param>
    /// <param name="wParam">ウィンドウメッセージ種別</param>
    /// <param name="lParam">MSLLHOOKSTRUCT へのポインタ</param>
    /// <returns>次のフックの戻り値</returns>
    private nint MouseHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var isInjected = (data.flags & Win32.LLMHF_INJECTED) != 0;
            var ts = Stopwatch.GetTimestamp();
            var highWord = (short)(data.mouseData >> 16);

            RawInputEvent? evt = (uint)wParam switch
            {
                Win32.WM_MOUSEMOVE => new(RawInputKind.MouseMove, 0, 0, false, default,
                    data.pt.x, data.pt.y, 0, false, isInjected, ts),
                Win32.WM_LBUTTONDOWN => MakeButton(RawInputKind.MouseDown, MouseButton.Left),
                Win32.WM_LBUTTONUP => MakeButton(RawInputKind.MouseUp, MouseButton.Left),
                Win32.WM_RBUTTONDOWN => MakeButton(RawInputKind.MouseDown, MouseButton.Right),
                Win32.WM_RBUTTONUP => MakeButton(RawInputKind.MouseUp, MouseButton.Right),
                Win32.WM_MBUTTONDOWN => MakeButton(RawInputKind.MouseDown, MouseButton.Middle),
                Win32.WM_MBUTTONUP => MakeButton(RawInputKind.MouseUp, MouseButton.Middle),
                Win32.WM_XBUTTONDOWN => MakeButton(RawInputKind.MouseDown,
                    highWord == Win32.XBUTTON1 ? MouseButton.X1 : MouseButton.X2),
                Win32.WM_XBUTTONUP => MakeButton(RawInputKind.MouseUp,
                    highWord == Win32.XBUTTON1 ? MouseButton.X1 : MouseButton.X2),
                Win32.WM_MOUSEWHEEL => new(RawInputKind.MouseWheel, 0, 0, false, default,
                    data.pt.x, data.pt.y, highWord, false, isInjected, ts),
                Win32.WM_MOUSEHWHEEL => new(RawInputKind.MouseWheel, 0, 0, false, default,
                    data.pt.x, data.pt.y, highWord, true, isInjected, ts),
                _ => null,
            };

            if (evt.HasValue)
            {
                _channel.Writer.TryWrite(evt.Value);
            }

            RawInputEvent MakeButton(RawInputKind kind, MouseButton button) =>
                new(kind, 0, 0, false, button, data.pt.x, data.pt.y, 0, false, isInjected, ts);
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    /// <summary>フックを解除し、フックスレッドと配信タスクを終了する</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_hookThread is not null)
        {
            NativeMethods.PostThreadMessage(_hookThreadId, Win32.WM_QUIT, 0, 0);
            _hookThread.Join(TimeSpan.FromSeconds(2));
            _hookThread = null;
        }
        _channel.Writer.TryComplete();
        _dispatchTask?.Wait(TimeSpan.FromSeconds(2));
    }
}
