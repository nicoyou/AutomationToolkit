using System.Windows;
using AutomationToolkit.App.ViewModels;

namespace AutomationToolkit.App.Views;

/// <summary>マクロ一覧と録画・再生操作を提供するメインウィンドウ</summary>
public partial class MainWindow : Window {
	/// <summary>メイン画面のビューモデル</summary>
	private readonly MainViewModel viewModel;

	/// <summary>ビューモデルをバインドしてウィンドウを初期化する</summary>
	/// <param name="viewModel">メイン画面のビューモデル</param>
	public MainWindow(MainViewModel viewModel) {
		InitializeComponent();
		this.viewModel = viewModel;
		DataContext = viewModel;
		Title = "AutomationToolkit";
		this.viewModel.StateChangedForTray += UpdateTitle;
	}

	/// <summary>動作状態に応じてウィンドウタイトルを更新する</summary>
	private void UpdateTitle() {
		Title = viewModel.State switch {
			AppState.Recording => "[REC] AutomationToolkit",
			AppState.Playing => "[再生中] AutomationToolkit",
			_ => "AutomationToolkit",
		};
	}

	/// <summary>ウィンドウを閉じるときにバインディングを保存する</summary>
	/// <param name="e">クローズイベントの引数</param>
	protected override void OnClosed(EventArgs e) {
		viewModel.PersistBindings();
		base.OnClosed(e);
	}
}
