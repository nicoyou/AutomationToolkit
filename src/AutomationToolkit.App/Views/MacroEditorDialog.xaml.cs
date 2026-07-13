using System.ComponentModel;
using System.Windows;
using AutomationToolkit.App.ViewModels;

namespace AutomationToolkit.App.Views;

/// <summary>マクロのステップを一括編集するダイアログ</summary>
public partial class MacroEditorDialog : Window {
	/// <summary>ビューモデルからの要求で閉じている最中かどうか</summary>
	private bool closeRequestedByViewModel;

	/// <summary>ビューモデルを受け取って初期化する</summary>
	/// <param name="viewModel">編集画面のビューモデル</param>
	public MacroEditorDialog(MacroEditorViewModel viewModel) {
		InitializeComponent();
		DataContext = viewModel;
		viewModel.CloseRequested += saved => {
			closeRequestedByViewModel = true;
			DialogResult = saved;
		};
	}

	/// <summary>テスト再生中は先に停止させ、未保存の変更があれば保存確認する</summary>
	/// <param name="e">クローズイベントの引数</param>
	protected override void OnClosing(CancelEventArgs e) {
		base.OnClosing(e);
		if (closeRequestedByViewModel) return;
		var viewModel = (MacroEditorViewModel)DataContext;
		if (viewModel.IsTestPlaying) {
			// 押しっぱなし解放が完了するまでウィンドウを破棄しないよう、停止だけ行い閉じるのは次回の操作に任せる
			viewModel.StopTestPlaybackCommand.Execute(null);
			e.Cancel = true;
			return;
		}
		if (viewModel.isDirty == false) return;
		var result = MessageBox.Show(
			"未保存の変更があります。保存して閉じますか?", "マクロ編集",
			MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
		if (result == MessageBoxResult.Cancel) {
			e.Cancel = true;
		}
		else if (result == MessageBoxResult.Yes) {
			// クローズ処理中の DialogResult 設定は再入例外になるため、完了後にコマンド経由で保存して閉じ直す
			e.Cancel = true;
			Dispatcher.BeginInvoke(() => viewModel.SaveAndCloseCommand.Execute(null));
		}
	}
}
