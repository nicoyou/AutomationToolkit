using System.IO;
using AutomationToolkit.App.Services;
using AutomationToolkit.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationToolkit.App.ViewModels;

/// <summary>マクロ一覧の 1 行分のビューモデル</summary>
public sealed partial class MacroItemViewModel : ObservableObject {
	/// <summary>マクロの表示名</summary>
	[ObservableProperty]
	private string name;
	/// <summary>再生ホットキー。null なら未設定</summary>
	[ObservableProperty]
	private HotkeyChord? hotkey;
	/// <summary>再生速度倍率</summary>
	[ObservableProperty]
	private double speed;
	/// <summary>ループ回数。0 で無限ループ</summary>
	[ObservableProperty]
	private int loopCount;

	/// <summary>マクロファイルのパス</summary>
	public string filePath { get; }
	/// <summary>マクロファイルのファイル名</summary>
	public string fileName { get; }

	/// <summary>ホットキーの表示用文字列</summary>
	public string hotkeyDisplay => Hotkey?.ToString() ?? "(未設定)";

	/// <summary>ファイルパスとバインディングから 1 行分の状態を組み立てる</summary>
	/// <param name="filePath">マクロファイルのパス</param>
	/// <param name="name">マクロの表示名</param>
	/// <param name="binding">このマクロのホットキー・再生設定</param>
	public MacroItemViewModel(string filePath, string name, MacroBinding binding) {
		this.filePath = filePath;
		fileName = Path.GetFileName(filePath);
		this.name = name;
		hotkey = binding.hotkey;
		speed = binding.speed;
		loopCount = binding.loopCount;
	}

	/// <summary>現在の状態を設定保存用のバインディングへ変換する</summary>
	/// <returns>このマクロのホットキー・再生設定</returns>
	public MacroBinding ToBinding() => new() {
		hotkey = Hotkey,
		speed = Speed,
		loopCount = LoopCount,
	};

	/// <summary>ホットキー変更時に表示用文字列の変更も通知する</summary>
	/// <param name="value">変更後のホットキー</param>
	partial void OnHotkeyChanged(HotkeyChord? value) => OnPropertyChanged(nameof(hotkeyDisplay));
}
