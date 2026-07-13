using AutomationToolkit.Core.Models;

namespace AutomationToolkit.App.Services;

/// <summary>マクロ 1 件ごとのホットキー・再生設定</summary>
public sealed class MacroBinding
{
    /// <summary>マクロを再生するホットキー。null なら未設定</summary>
    public HotkeyChord? Hotkey { get; set; }

    /// <summary>再生速度倍率</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>ループ回数</summary>
    /// <remarks>0 で無限ループ</remarks>
    public int LoopCount { get; set; } = 1;
}

/// <summary>アプリ全体の設定</summary>
public sealed class AppSettings
{
    /// <summary>マクロファイルを保存するフォルダのパス。null なら既定のフォルダ</summary>
    public string? MacrosFolder { get; set; }

    /// <summary>録画の開始・停止をトグルするホットキー</summary>
    public HotkeyChord RecordHotkey { get; set; } = new(ChordModifiers.Control | ChordModifiers.Alt, 0x52); // Ctrl+Alt+R

    /// <summary>再生を停止するホットキー</summary>
    public HotkeyChord StopHotkey { get; set; } = new(ChordModifiers.Control | ChordModifiers.Alt, 0x53); // Ctrl+Alt+S

    /// <summary>マクロファイル名からバインディングへの対応表</summary>
    public Dictionary<string, MacroBinding> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
