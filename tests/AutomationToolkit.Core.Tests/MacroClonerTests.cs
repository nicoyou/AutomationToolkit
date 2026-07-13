using AutomationToolkit.Core.Editing;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Tests;

/// <summary>MacroCloner の複製のテスト</summary>
public class MacroClonerTests {
	/// <summary>複製後に元のステップを変更してもクローンへ影響しない</summary>
	[Fact]
	public void CloneSteps_IsIndependentFromOriginal() {
		var original = new List<MacroStep> {
			new MouseDownStep { button = MouseButton.Left, x = 100, y = 200, delayBeforeMs = 50 },
		};

		var clone = MacroCloner.CloneSteps(original);
		original[0].delayBeforeMs = 999;
		((MouseDownStep)original[0]).x = 999;

		var cloned = Assert.IsType<MouseDownStep>(Assert.Single(clone));
		Assert.Equal(50, cloned.delayBeforeMs);
		Assert.Equal(100, cloned.x);
	}

	/// <summary>全ての派生型が型と内容を保ったまま複製される</summary>
	[Fact]
	public void CloneSteps_PreservesAllDerivedTypes() {
		var original = new List<MacroStep> {
			new KeyDownStep { virtualKey = 65, scanCode = 30, isExtended = true, delayBeforeMs = 1 },
			new KeyUpStep { virtualKey = 66, scanCode = 48, delayBeforeMs = 2 },
			new MouseMoveStep { x = 1, y = 2, delayBeforeMs = 3 },
			new MouseDownStep { button = MouseButton.Right, x = 3, y = 4, delayBeforeMs = 4 },
			new MouseUpStep { button = MouseButton.Right, x = 5, y = 6, delayBeforeMs = 5 },
			new MouseWheelStep { x = 7, y = 8, delta = -120, isHorizontal = true, delayBeforeMs = 6 },
		};

		var clone = MacroCloner.CloneSteps(original);

		Assert.Equal(original.Count, clone.Count);
		var keyDown = Assert.IsType<KeyDownStep>(clone[0]);
		Assert.Equal(65, keyDown.virtualKey);
		Assert.True(keyDown.isExtended);
		Assert.IsType<KeyUpStep>(clone[1]);
		Assert.IsType<MouseMoveStep>(clone[2]);
		var down = Assert.IsType<MouseDownStep>(clone[3]);
		Assert.Equal(MouseButton.Right, down.button);
		Assert.IsType<MouseUpStep>(clone[4]);
		var wheel = Assert.IsType<MouseWheelStep>(clone[5]);
		Assert.Equal(-120, wheel.delta);
		Assert.True(wheel.isHorizontal);
		Assert.Equal([1, 2, 3, 4, 5, 6], clone.Select(step => step.delayBeforeMs));
	}
}
