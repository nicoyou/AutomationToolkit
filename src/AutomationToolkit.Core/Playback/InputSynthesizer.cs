using AutomationToolkit.Core.Interop;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Playback;

/// <summary>SendInput によるマウス・キーボード入力の合成を担当する</summary>
/// <remarks>座標は送出時に 0-65535 の仮想スクリーン絶対座標へ正規化する</remarks>
public sealed class InputSynthesizer {
	/// <summary>仮想スクリーンの左端座標</summary>
	private int virtualScreenLeft;
	/// <summary>仮想スクリーンの上端座標</summary>
	private int virtualScreenTop;
	/// <summary>仮想スクリーンの幅</summary>
	private int virtualScreenWidth;
	/// <summary>仮想スクリーンの高さ</summary>
	private int virtualScreenHeight;

	/// <summary>仮想スクリーンの範囲を取得して初期化する</summary>
	public InputSynthesizer() => RefreshVirtualScreenMetrics();

	/// <summary>仮想スクリーンの範囲を再取得する</summary>
	/// <remarks>録画時とモニタ構成が変わっている可能性があるため、再生セッション開始ごとに呼ぶ</remarks>
	public void RefreshVirtualScreenMetrics() {
		virtualScreenLeft = NativeMethods.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
		virtualScreenTop = NativeMethods.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
		virtualScreenWidth = Math.Max(NativeMethods.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN), 2);
		virtualScreenHeight = Math.Max(NativeMethods.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN), 2);
	}

	/// <summary>ステップに対応する入力を送出する</summary>
	/// <param name="step">送出する操作ステップ</param>
	/// <exception cref="NotSupportedException">未対応のステップ型の場合</exception>
	public void Send(MacroStep step) {
		switch (step) {
			case MouseMoveStep m:
				SendInputs([MakeMouseMove(m.x, m.y)]);
				break;
			case MouseDownStep d:
				// 間引きで直前の move が落ちていてもクリック位置を保証するため、move を同時送出する
				SendInputs([MakeMouseMove(d.x, d.y), MakeMouseButton(d.button, down: true)]);
				break;
			case MouseUpStep u:
				SendInputs([MakeMouseMove(u.x, u.y), MakeMouseButton(u.button, down: false)]);
				break;
			case MouseWheelStep w:
				SendInputs([MakeMouseMove(w.x, w.y), MakeWheel(w.delta, w.isHorizontal)]);
				break;
			case KeyDownStep kd:
				SendKey(kd.virtualKey, kd.scanCode, kd.isExtended, down: true);
				break;
			case KeyUpStep ku:
				SendKey(ku.virtualKey, ku.scanCode, ku.isExtended, down: false);
				break;
			default:
				throw new NotSupportedException($"未対応のステップ型です: {step.GetType().Name}");
		}
	}

	/// <summary>キー入力を送出する</summary>
	/// <param name="virtualKey">Win32 仮想キーコード</param>
	/// <param name="scanCode">ハードウェアスキャンコード。0 なら virtualKey で送出する</param>
	/// <param name="isExtended">拡張キーかどうか</param>
	/// <param name="down">true なら押下、false なら解放</param>
	public void SendKey(ushort virtualKey, ushort scanCode, bool isExtended, bool down) {
		var ki = new KEYBDINPUT();
		if (scanCode != 0) {
			// スキャンコード送出 ( DirectInput 系のゲームにも入力が届きやすい )
			ki.wScan = scanCode;
			ki.dwFlags = Win32.KEYEVENTF_SCANCODE;
		}
		else {
			ki.wVk = virtualKey;
		}
		if (isExtended) ki.dwFlags |= Win32.KEYEVENTF_EXTENDEDKEY;
		if (down == false) ki.dwFlags |= Win32.KEYEVENTF_KEYUP;
		SendInputs([new INPUT { type = Win32.INPUT_KEYBOARD, U = new InputUnion { ki = ki } }]);
	}

	/// <summary>マウスボタンの解放入力を送出する</summary>
	/// <param name="button">解放するマウスボタン</param>
	public void SendMouseButtonUp(MouseButton button)
		=> SendInputs([MakeMouseButton(button, down: false)]);

	/// <summary>カーソル移動の入力データを生成する</summary>
	/// <param name="x">仮想スクリーン空間の X 座標</param>
	/// <param name="y">仮想スクリーン空間の Y 座標</param>
	/// <returns>カーソル移動の INPUT</returns>
	private INPUT MakeMouseMove(int x, int y) {
		var normalizedX = (int)Math.Round((x - virtualScreenLeft) * 65535.0 / (virtualScreenWidth - 1));
		var normalizedY = (int)Math.Round((y - virtualScreenTop) * 65535.0 / (virtualScreenHeight - 1));
		return new INPUT {
			type = Win32.INPUT_MOUSE,
			U = new InputUnion {
				mi = new MOUSEINPUT {
					dx = Math.Clamp(normalizedX, 0, 65535),
					dy = Math.Clamp(normalizedY, 0, 65535),
					dwFlags = Win32.MOUSEEVENTF_MOVE | Win32.MOUSEEVENTF_ABSOLUTE | Win32.MOUSEEVENTF_VIRTUALDESK,
				},
			},
		};
	}

	/// <summary>マウスボタン操作の入力データを生成する</summary>
	/// <param name="button">対象のマウスボタン</param>
	/// <param name="down">true なら押下、false なら解放</param>
	/// <returns>マウスボタン操作の INPUT</returns>
	/// <exception cref="ArgumentOutOfRangeException">未知のボタン種別の場合</exception>
	private static INPUT MakeMouseButton(MouseButton button, bool down) {
		var (flags, data) = (button, down) switch {
			(MouseButton.Left, true) => (Win32.MOUSEEVENTF_LEFTDOWN, 0u),
			(MouseButton.Left, false) => (Win32.MOUSEEVENTF_LEFTUP, 0u),
			(MouseButton.Right, true) => (Win32.MOUSEEVENTF_RIGHTDOWN, 0u),
			(MouseButton.Right, false) => (Win32.MOUSEEVENTF_RIGHTUP, 0u),
			(MouseButton.Middle, true) => (Win32.MOUSEEVENTF_MIDDLEDOWN, 0u),
			(MouseButton.Middle, false) => (Win32.MOUSEEVENTF_MIDDLEUP, 0u),
			(MouseButton.X1, true) => (Win32.MOUSEEVENTF_XDOWN, Win32.XBUTTON1),
			(MouseButton.X1, false) => (Win32.MOUSEEVENTF_XUP, Win32.XBUTTON1),
			(MouseButton.X2, true) => (Win32.MOUSEEVENTF_XDOWN, Win32.XBUTTON2),
			(MouseButton.X2, false) => (Win32.MOUSEEVENTF_XUP, Win32.XBUTTON2),
			_ => throw new ArgumentOutOfRangeException(nameof(button)),
		};
		return new INPUT {
			type = Win32.INPUT_MOUSE,
			U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags, mouseData = data } },
		};
	}

	/// <summary>ホイール回転の入力データを生成する</summary>
	/// <param name="delta">1 ノッチを ±120 とする回転量</param>
	/// <param name="horizontal">水平ホイールかどうか</param>
	/// <returns>ホイール回転の INPUT</returns>
	private static INPUT MakeWheel(int delta, bool horizontal) => new() {
		type = Win32.INPUT_MOUSE,
		U = new InputUnion {
			mi = new MOUSEINPUT {
				dwFlags = horizontal ? Win32.MOUSEEVENTF_HWHEEL : Win32.MOUSEEVENTF_WHEEL,
				mouseData = unchecked((uint)delta),
			},
		},
	};

	/// <summary>入力の配列を SendInput で送出する</summary>
	/// <param name="inputs">送出する入力の配列</param>
	/// <exception cref="InvalidOperationException">一部でも送出がブロックされた場合</exception>
	private static void SendInputs(INPUT[] inputs) {
		var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, INPUT.Size);
		if (sent != inputs.Length) {
			throw new InvalidOperationException(
				$"SendInput が入力をブロックしました (送出 {sent}/{inputs.Length} 件)。" +
				"対象アプリが管理者権限で動作している場合は本ツールも管理者として実行してください。");
		}
	}
}
