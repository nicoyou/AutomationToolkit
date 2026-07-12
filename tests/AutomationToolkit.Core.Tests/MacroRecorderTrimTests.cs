using AutomationToolkit.Core.Models;
using AutomationToolkit.Core.Recording;

namespace AutomationToolkit.Core.Tests;

/// <summary>MacroRecorder のホットキー残骸トリムのテスト</summary>
public class MacroRecorderTrimTests
{
    /// <summary>Ctrl の仮想キーコード</summary>
    private const ushort VkControl = 0x11;
    /// <summary>Alt の仮想キーコード</summary>
    private const ushort VkMenu = 0x12;
    /// <summary>A キーの仮想キーコード</summary>
    private const ushort VkA = 0x41;

    /// <summary>先頭の孤立した KeyUp が除去され、待機時間が次のステップへ引き継がれる</summary>
    [Fact]
    public void LeadingOrphanKeyUp_IsRemoved_AndDelayCarriedOver()
    {
        // 録画開始ホットキー (Ctrl+Alt+R) の離しが先頭に残ったケース
        var steps = new List<MacroStep>
        {
            new KeyUpStep { VirtualKey = VkControl, DelayBeforeMs = 80 },
            new KeyUpStep { VirtualKey = VkMenu, DelayBeforeMs = 20 },
            new KeyDownStep { VirtualKey = VkA, DelayBeforeMs = 500 },
            new KeyUpStep { VirtualKey = VkA, DelayBeforeMs = 60 },
        };

        MacroRecorder.TrimHotkeyArtifacts(steps);

        Assert.Equal(2, steps.Count);
        var down = Assert.IsType<KeyDownStep>(steps[0]);
        Assert.Equal(VkA, down.VirtualKey);
        Assert.Equal(600, down.DelayBeforeMs); // 80 + 20 + 500
    }

    /// <summary>末尾の対応する KeyUp がない修飾キーの KeyDown が除去される</summary>
    [Fact]
    public void TrailingModifierKeyDownWithoutKeyUp_IsRemoved()
    {
        // 停止ホットキーの Ctrl+Alt 押し込みが末尾に残ったケース (メインキーはフックで飲み込み済み)
        var steps = new List<MacroStep>
        {
            new KeyDownStep { VirtualKey = VkA },
            new KeyUpStep { VirtualKey = VkA, DelayBeforeMs = 50 },
            new KeyDownStep { VirtualKey = VkControl, DelayBeforeMs = 300 },
            new KeyDownStep { VirtualKey = VkMenu, DelayBeforeMs = 30 },
        };

        MacroRecorder.TrimHotkeyArtifacts(steps);

        Assert.Equal(2, steps.Count);
        Assert.IsType<KeyDownStep>(steps[0]);
        Assert.IsType<KeyUpStep>(steps[1]);
    }

    /// <summary>通常のキー操作列は変更されない</summary>
    [Fact]
    public void NormalKeySequence_IsUntouched()
    {
        var steps = new List<MacroStep>
        {
            new KeyDownStep { VirtualKey = VkControl },        // 意図的な Ctrl+C
            new KeyDownStep { VirtualKey = 0x43, DelayBeforeMs = 100 },
            new KeyUpStep { VirtualKey = 0x43, DelayBeforeMs = 50 },
            new KeyUpStep { VirtualKey = VkControl, DelayBeforeMs = 30 },
            new MouseMoveStep { X = 10, Y = 20, DelayBeforeMs = 200 },
        };

        MacroRecorder.TrimHotkeyArtifacts(steps);

        Assert.Equal(5, steps.Count);
    }

    /// <summary>マウス操作のみの録画は変更されない</summary>
    [Fact]
    public void MouseOnlyRecording_IsUntouched()
    {
        var steps = new List<MacroStep>
        {
            new MouseMoveStep { X = 1, Y = 2 },
            new MouseDownStep { Button = MouseButton.Left, X = 1, Y = 2, DelayBeforeMs = 30 },
            new MouseUpStep { Button = MouseButton.Left, X = 1, Y = 2, DelayBeforeMs = 40 },
        };

        MacroRecorder.TrimHotkeyArtifacts(steps);

        Assert.Equal(3, steps.Count);
    }
}
