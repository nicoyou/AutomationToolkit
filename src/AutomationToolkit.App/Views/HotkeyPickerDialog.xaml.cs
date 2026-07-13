using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.App.Views;

/// <summary>押されたキーコンボをそのままホットキーとして設定するダイアログ</summary>
public partial class HotkeyPickerDialog : Window {
	/// <summary>選択されたホットキー。null はクリアを表す</summary>
	public HotkeyChord? chord { get; private set; }

	/// <summary>現在の設定値を初期表示する</summary>
	/// <param name="current">現在のホットキー。null なら未設定</param>
	public HotkeyPickerDialog(HotkeyChord? current) {
		InitializeComponent();
		chord = current;
		UpdateText();
	}

	/// <summary>現在のホットキーの表示を更新する</summary>
	private void UpdateText() => ChordText.Text = chord?.ToString() ?? "(未設定)";

	/// <summary>押されたキーコンボをホットキーとして取り込む</summary>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="e">キーイベントの引数</param>
	private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
		var key = e.Key == Key.System ? e.SystemKey : e.Key;

		// 修飾キー単体はメインキーとして扱わない
		if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) {
			return;
		}
		if (key is Key.Escape or Key.Enter or Key.Tab) return; // ダイアログ操作用に確保

		e.Handled = true;
		var modifiers = ChordModifiers.None;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= ChordModifiers.Control;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= ChordModifiers.Alt;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= ChordModifiers.Shift;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= ChordModifiers.Win;

		var virtualKeyCode = (ushort)KeyInterop.VirtualKeyFromKey(key);
		chord = new HotkeyChord(modifiers, virtualKeyCode);
		UpdateText();
	}

	/// <summary>ホットキーをクリアする</summary>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="e">クリックイベントの引数</param>
	private void Clear_Click(object sender, RoutedEventArgs e) {
		chord = null;
		UpdateText();
	}

	/// <summary>選択内容を確定してダイアログを閉じる</summary>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="e">クリックイベントの引数</param>
	private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
