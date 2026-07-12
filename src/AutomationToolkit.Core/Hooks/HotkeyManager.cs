using AutomationToolkit.Core.Interop;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Hooks;

/// <summary>グローバルホットキーの登録・解除を提供する</summary>
public interface IHotkeyManager
{
    /// <summary>ホットキーを登録する</summary>
    /// <param name="chord">検出するキーコンボ</param>
    /// <param name="callback">検出時に呼ぶコールバック</param>
    /// <param name="swallow">検出したキーをフォアグラウンドアプリに渡さず飲み込むかどうか</param>
    /// <returns>解除に使う登録 ID</returns>
    Guid Register(HotkeyChord chord, Action callback, bool swallow = true);

    /// <summary>ホットキーの登録を解除する</summary>
    /// <param name="id">Register が返した登録 ID</param>
    void Unregister(Guid id);
}

/// <summary>低レベルキーボードフックによるグローバルホットキー検出</summary>
/// <remarks>
/// RegisterHotKey と違い他アプリとの登録競合がなく、録画中でも動作する。
/// swallow 時はマッチしたキーがフォアグラウンドアプリにも録画にも渡らない
/// </remarks>
public sealed class HotkeyManager : IHotkeyManager, IDisposable
{
    /// <summary>1 件のホットキー登録</summary>
    /// <param name="Id">登録 ID</param>
    /// <param name="Chord">検出するキーコンボ</param>
    /// <param name="Callback">検出時に呼ぶコールバック</param>
    /// <param name="Swallow">検出したキーを飲み込むかどうか</param>
    private sealed record Registration(Guid Id, HotkeyChord Chord, Action Callback, bool Swallow);

    /// <summary>キーイベントの供給元となる入力フック</summary>
    private readonly LowLevelInputHook _hook;

    /// <summary>登録一覧の更新を直列化するロック</summary>
    private readonly Lock _gate = new();

    /// <summary>現在の登録一覧。読み取りはロックなしで行うためイミュータブルに差し替える</summary>
    private volatile Registration[] _registrations = [];

    /// <summary>押下中のホットキーメインキー。オートリピートの多重発火防止に使う</summary>
    /// <remarks>フックスレッドからのみ触る</remarks>
    private readonly HashSet<ushort> _downHotkeyKeys = [];

    /// <summary>入力フックにキーイベントフィルタを取り付ける</summary>
    /// <param name="hook">キーイベントの供給元となる入力フック</param>
    public HotkeyManager(LowLevelInputHook hook)
    {
        _hook = hook;
        _hook.KeyEventFilter = OnKeyEvent;
    }

    /// <inheritdoc/>
    public Guid Register(HotkeyChord chord, Action callback, bool swallow = true)
    {
        var registration = new Registration(Guid.NewGuid(), chord, callback, swallow);
        lock (_gate)
        {
            _registrations = [.. _registrations, registration];
        }
        return registration.Id;
    }

    /// <inheritdoc/>
    public void Unregister(Guid id)
    {
        lock (_gate)
        {
            _registrations = _registrations.Where(r => r.Id != id).ToArray();
        }
    }

    /// <summary>キーイベントを判定し、登録済みホットキーにマッチしたらコールバックを発火する</summary>
    /// <remarks>フックスレッド上で呼ばれる</remarks>
    /// <param name="e">判定するキーイベント</param>
    /// <returns>true ならイベントを飲み込む</returns>
    private bool OnKeyEvent(RawInputEvent e)
    {
        if (e.IsInjected)
        {
            return false; // 再生中の合成入力でホットキーを発火させない
        }

        if (e.Kind == RawInputKind.KeyUp)
        {
            // 飲み込んだ down と対になる up も飲み込む (アプリに孤立した up を渡さない)
            return _downHotkeyKeys.Remove(e.VirtualKey);
        }

        var registrations = _registrations;
        if (registrations.Length == 0)
        {
            return false;
        }

        var modifiers = GetCurrentModifiers();
        foreach (var r in registrations)
        {
            if (r.Chord.VirtualKey == e.VirtualKey && r.Chord.Modifiers == modifiers)
            {
                // オートリピート中は再発火させない
                if (_downHotkeyKeys.Add(e.VirtualKey))
                {
                    ThreadPool.QueueUserWorkItem(static cb => ((Action)cb!)(), r.Callback);
                }
                return r.Swallow;
            }
        }
        return false;
    }

    /// <summary>現在押下されている修飾キーの組み合わせを取得する</summary>
    /// <returns>押下中の修飾キーの組み合わせ</returns>
    private static ChordModifiers GetCurrentModifiers()
    {
        var modifiers = ChordModifiers.None;
        if (IsDown(Win32.VK_CONTROL)) modifiers |= ChordModifiers.Control;
        if (IsDown(Win32.VK_MENU)) modifiers |= ChordModifiers.Alt;
        if (IsDown(Win32.VK_SHIFT)) modifiers |= ChordModifiers.Shift;
        if (IsDown(Win32.VK_LWIN) || IsDown(Win32.VK_RWIN)) modifiers |= ChordModifiers.Win;
        return modifiers;

        static bool IsDown(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    /// <summary>フィルタを取り外し、すべての登録を破棄する</summary>
    public void Dispose()
    {
        _hook.KeyEventFilter = null;
        _registrations = [];
    }
}
