using AutomationToolkit.Core.Models;
using AutomationToolkit.Core.Recording;

namespace AutomationToolkit.Core.Tests;

/// <summary>MacroRecorder のホットキー残骸トリムのテスト</summary>
public class MacroRecorderTrimTests {
	/// <summary>Ctrl の仮想キーコード</summary>
	private const ushort VK_CONTROL = 0x11;
	/// <summary>Alt の仮想キーコード</summary>
	private const ushort VK_MENU = 0x12;
	/// <summary>A キーの仮想キーコード</summary>
	private const ushort VK_A = 0x41;

	/// <summary>先頭の孤立した KeyUp が除去され、待機時間が次のステップへ引き継がれる</summary>
	[Fact]
	public void LeadingOrphanKeyUp_IsRemoved_AndDelayCarriedOver() {
		// 録画開始ホットキー ( Ctrl+Alt+R ) の離しが先頭に残ったケース
		var steps = new List<MacroStep> {
			new KeyUpStep { virtualKey = VK_CONTROL, delayBeforeMs = 80 },
			new KeyUpStep { virtualKey = VK_MENU, delayBeforeMs = 20 },
			new KeyDownStep { virtualKey = VK_A, delayBeforeMs = 500 },
			new KeyUpStep { virtualKey = VK_A, delayBeforeMs = 60 },
		};

		MacroRecorder.TrimHotkeyArtifacts(steps);

		Assert.Equal(2, steps.Count);
		var down = Assert.IsType<KeyDownStep>(steps[0]);
		Assert.Equal(VK_A, down.virtualKey);
		Assert.Equal(600, down.delayBeforeMs); // 除去された各ステップの待機時間が合算される
	}

	/// <summary>末尾の対応する KeyUp がない修飾キーの KeyDown が除去される</summary>
	[Fact]
	public void TrailingModifierKeyDownWithoutKeyUp_IsRemoved() {
		// 停止ホットキーの Ctrl+Alt 押し込みが末尾に残ったケース ( メインキーはフックで飲み込み済み )
		var steps = new List<MacroStep> {
			new KeyDownStep { virtualKey = VK_A },
			new KeyUpStep { virtualKey = VK_A, delayBeforeMs = 50 },
			new KeyDownStep { virtualKey = VK_CONTROL, delayBeforeMs = 300 },
			new KeyDownStep { virtualKey = VK_MENU, delayBeforeMs = 30 },
		};

		MacroRecorder.TrimHotkeyArtifacts(steps);

		Assert.Equal(2, steps.Count);
		Assert.IsType<KeyDownStep>(steps[0]);
		Assert.IsType<KeyUpStep>(steps[1]);
	}

	/// <summary>通常のキー操作列は変更されない</summary>
	[Fact]
	public void NormalKeySequence_IsUntouched() {
		var steps = new List<MacroStep> {
			new KeyDownStep { virtualKey = VK_CONTROL },        // 意図的な Ctrl+C
			new KeyDownStep { virtualKey = 0x43, delayBeforeMs = 100 },
			new KeyUpStep { virtualKey = 0x43, delayBeforeMs = 50 },
			new KeyUpStep { virtualKey = VK_CONTROL, delayBeforeMs = 30 },
			new MouseMoveStep { x = 10, y = 20, delayBeforeMs = 200 },
		};

		MacroRecorder.TrimHotkeyArtifacts(steps);

		Assert.Equal(5, steps.Count);
	}

	/// <summary>マウス操作のみの録画は変更されない</summary>
	[Fact]
	public void MouseOnlyRecording_IsUntouched() {
		var steps = new List<MacroStep> {
			new MouseMoveStep { x = 1, y = 2 },
			new MouseDownStep { button = MouseButton.Left, x = 1, y = 2, delayBeforeMs = 30 },
			new MouseUpStep { button = MouseButton.Left, x = 1, y = 2, delayBeforeMs = 40 },
		};

		MacroRecorder.TrimHotkeyArtifacts(steps);

		Assert.Equal(3, steps.Count);
	}
}
