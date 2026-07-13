using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Hooks;

/// <summary>生の入力イベントの種別</summary>
public enum RawInputKind {
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
/// <param name="kind">イベントの種別</param>
/// <param name="virtualKey">キーイベントの仮想キーコード</param>
/// <param name="scanCode">キーイベントのハードウェアスキャンコード</param>
/// <param name="isExtended">拡張キーかどうか</param>
/// <param name="button">マウスボタンイベントの対象ボタン</param>
/// <param name="x">マウスイベントのカーソル X 座標</param>
/// <param name="y">マウスイベントのカーソル Y 座標</param>
/// <param name="wheelDelta">1 ノッチを ±120 とするホイール回転量</param>
/// <param name="isHorizontalWheel">水平ホイールかどうか</param>
/// <param name="isInjected">SendInput などで合成された入力かどうか</param>
/// <param name="timestampTicks">イベント発生時刻の Stopwatch タイムスタンプ</param>
public readonly record struct RawInputEvent(
	RawInputKind kind,
	ushort virtualKey,
	ushort scanCode,
	bool isExtended,
	MouseButton button,
	int x,
	int y,
	int wheelDelta,
	bool isHorizontalWheel,
	bool isInjected,
	long timestampTicks);
