using System.IO;
using AutomationToolkit.App.Services;
using AutomationToolkit.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationToolkit.App.ViewModels;

/// <summary>マクロ一覧の 1 行分のビューモデル</summary>
public sealed partial class MacroItemViewModel : ObservableObject
{
    /// <summary>マクロファイルのパス</summary>
    public string FilePath { get; }

    /// <summary>マクロファイルのファイル名</summary>
    public string FileName { get; }

    /// <summary>マクロの表示名</summary>
    [ObservableProperty]
    private string _name;

    /// <summary>再生ホットキー。null なら未設定</summary>
    [ObservableProperty]
    private HotkeyChord? _hotkey;

    /// <summary>再生速度倍率</summary>
    [ObservableProperty]
    private double _speed;

    /// <summary>ループ回数。0 で無限ループ</summary>
    [ObservableProperty]
    private int _loopCount;

    /// <summary>ファイルパスとバインディングから 1 行分の状態を組み立てる</summary>
    /// <param name="filePath">マクロファイルのパス</param>
    /// <param name="name">マクロの表示名</param>
    /// <param name="binding">このマクロのホットキー・再生設定</param>
    public MacroItemViewModel(string filePath, string name, MacroBinding binding)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        _name = name;
        _hotkey = binding.Hotkey;
        _speed = binding.Speed;
        _loopCount = binding.LoopCount;
    }

    /// <summary>ホットキーの表示用文字列</summary>
    public string HotkeyDisplay => Hotkey?.ToString() ?? "(未設定)";

    /// <summary>ホットキー変更時に表示用文字列の変更も通知する</summary>
    /// <param name="value">変更後のホットキー</param>
    partial void OnHotkeyChanged(HotkeyChord? value) => OnPropertyChanged(nameof(HotkeyDisplay));

    /// <summary>現在の状態を設定保存用のバインディングへ変換する</summary>
    /// <returns>このマクロのホットキー・再生設定</returns>
    public MacroBinding ToBinding() => new()
    {
        Hotkey = Hotkey,
        Speed = Speed,
        LoopCount = LoopCount,
    };
}
