using AutomationToolkit.Core.Interop;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Hooks;

/// <summary>グローバルホットキーの登録・解除を提供する</summary>
public interface IHotkeyManager {
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
public sealed class HotkeyManager : IHotkeyManager, IDisposable {
	/// <summary>1 件のホットキー登録</summary>
	/// <param name="id">登録 ID</param>
	/// <param name="chord">検出するキーコンボ</param>
	/// <param name="callback">検出時に呼ぶコールバック</param>
	/// <param name="swallow">検出したキーを飲み込むかどうか</param>
	private sealed record Registration(Guid id, HotkeyChord chord, Action callback, bool swallow);

	/// <summary>キーイベントの供給元となる入力フック</summary>
	private readonly LowLevelInputHook hook;
	/// <summary>登録一覧の更新を直列化するロック</summary>
	private readonly Lock gate = new();
	/// <summary>現在の登録一覧。読み取りはロックなしで行うためイミュータブルに差し替える</summary>
	private volatile Registration[] registrations = [];
	/// <summary>押下中のホットキーメインキー。オートリピートの多重発火防止に使う</summary>
	/// <remarks>フックスレッドからのみ触る</remarks>
	private readonly HashSet<ushort> downHotkeyKeys = [];

	/// <summary>入力フックにキーイベントフィルタを取り付ける</summary>
	/// <param name="hook">キーイベントの供給元となる入力フック</param>
	public HotkeyManager(LowLevelInputHook hook) {
		this.hook = hook;
		this.hook.KeyEventFilter = OnKeyEvent;
	}

	/// <inheritdoc/>
	public Guid Register(HotkeyChord chord, Action callback, bool swallow = true) {
		var registration = new Registration(Guid.NewGuid(), chord, callback, swallow);
		lock (gate) {
			registrations = [.. registrations, registration];
		}
		return registration.id;
	}

	/// <inheritdoc/>
	public void Unregister(Guid id) {
		lock (gate) {
			registrations = registrations.Where(r => r.id != id).ToArray();
		}
	}

	/// <summary>キーイベントを判定し、登録済みホットキーにマッチしたらコールバックを発火する</summary>
	/// <remarks>フックスレッド上で呼ばれる</remarks>
	/// <param name="inputEvent">判定するキーイベント</param>
	/// <returns>true ならイベントを飲み込む</returns>
	private bool OnKeyEvent(RawInputEvent inputEvent) {
		// 再生中の合成入力でホットキーを発火させない
		if (inputEvent.isInjected) return false;

		// 飲み込んだ down と対になる up も飲み込む ( アプリに孤立した up を渡さない )
		if (inputEvent.kind == RawInputKind.KeyUp) return downHotkeyKeys.Remove(inputEvent.virtualKey);

		var currentRegistrations = registrations;
		if (currentRegistrations.Length == 0) return false;

		var modifiers = GetCurrentModifiers();
		foreach (var r in currentRegistrations) {
			if (r.chord.virtualKey == inputEvent.virtualKey && r.chord.modifiers == modifiers) {
				// オートリピート中は再発火させない
				if (downHotkeyKeys.Add(inputEvent.virtualKey)) {
					ThreadPool.QueueUserWorkItem(static state => ((Action)state!)(), r.callback);
				}
				return r.swallow;
			}
		}
		return false;
	}

	/// <summary>現在押下されている修飾キーの組み合わせを取得する</summary>
	/// <returns>押下中の修飾キーの組み合わせ</returns>
	private static ChordModifiers GetCurrentModifiers() {
		var modifiers = ChordModifiers.None;
		if (IsDown(Win32.VK_CONTROL)) modifiers |= ChordModifiers.Control;
		if (IsDown(Win32.VK_MENU)) modifiers |= ChordModifiers.Alt;
		if (IsDown(Win32.VK_SHIFT)) modifiers |= ChordModifiers.Shift;
		if (IsDown(Win32.VK_LWIN) || IsDown(Win32.VK_RWIN)) modifiers |= ChordModifiers.Win;
		return modifiers;

		static bool IsDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
	}

	/// <summary>フィルタを取り外し、すべての登録を破棄する</summary>
	public void Dispose() {
		hook.KeyEventFilter = null;
		registrations = [];
	}
}
