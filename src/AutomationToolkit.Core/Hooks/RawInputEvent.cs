using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Hooks;

/// <summary>生の入力イベントの種別</summary>
public enum RawInputKind
{
    /// <summary>キーの押下</summary>
    KeyDown,
    /// <summary>キーの解放</summary>
    KeyUp,
    /// <summary>マウスボタンの押下</summary>
    MouseDown,
    /// <summary>マウスボタンの解放</summary>
    MouseUp,
    /// <summary>カーソルの移動</summary>
    MouseMove,
    /// <summary>ホイールの回転</summary>
    MouseWheel,
}

/// <summary>低レベルフックから届く生の入力イベント</summary>
/// <remarks>フックコールバック内で構築される軽量な構造体</remarks>
/// <param name="Kind">イベントの種別</param>
/// <param name="VirtualKey">キーイベントの仮想キーコード</param>
/// <param name="ScanCode">キーイベントのハードウェアスキャンコード</param>
/// <param name="IsExtended">拡張キーかどうか</param>
/// <param name="Button">マウスボタンイベントの対象ボタン</param>
/// <param name="X">マウスイベントのカーソル X 座標</param>
/// <param name="Y">マウスイベントのカーソル Y 座標</param>
/// <param name="WheelDelta">1 ノッチを ±120 とするホイール回転量</param>
/// <param name="IsHorizontalWheel">水平ホイールかどうか</param>
/// <param name="IsInjected">SendInput などで合成された入力かどうか</param>
/// <param name="TimestampTicks">イベント発生時刻の Stopwatch タイムスタンプ</param>
public readonly record struct RawInputEvent(
    RawInputKind Kind,
    ushort VirtualKey,
    ushort ScanCode,
    bool IsExtended,
    MouseButton Button,
    int X,
    int Y,
    int WheelDelta,
    bool IsHorizontalWheel,
    bool IsInjected,
    long TimestampTicks);
