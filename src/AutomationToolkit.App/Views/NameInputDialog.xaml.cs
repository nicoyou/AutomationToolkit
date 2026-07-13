using System.Windows;
using System.Windows.Input;

namespace AutomationToolkit.App.Views;

/// <summary>保存するマクロの名前を入力するダイアログ</summary>
public partial class NameInputDialog : Window {
	/// <summary>入力されたマクロ名</summary>
	public string enteredName { get; private set; }

	/// <summary>日時ベースの既定名を設定して初期化する</summary>
	public NameInputDialog() {
		InitializeComponent();
		enteredName = $"macro-{DateTime.Now:yyyyMMdd-HHmmss}";
		NameBox.Text = enteredName;
		Loaded += (_, _) => {
			NameBox.SelectAll();
			NameBox.Focus();
		};
	}

	/// <summary>空白でなければ入力内容を確定してダイアログを閉じる</summary>
	private void Accept() {
		if (string.IsNullOrWhiteSpace(NameBox.Text)) return;
		enteredName = NameBox.Text.Trim();
		DialogResult = true;
	}

	/// <summary>入力内容を確定する</summary>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="e">クリックイベントの引数</param>
	private void Ok_Click(object sender, RoutedEventArgs e) => Accept();

	/// <summary>Enter キーで入力内容を確定する</summary>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="e">キーイベントの引数</param>
	private void NameBox_KeyDown(object sender, KeyEventArgs e) {
		if (e.Key == Key.Enter) Accept();
	}
}
