using System.Windows;
using AutomationToolkit.App.Services;
using AutomationToolkit.App.ViewModels;
using AutomationToolkit.App.Views;
using AutomationToolkit.Core.Hooks;
using AutomationToolkit.Core.Playback;
using AutomationToolkit.Core.Recording;

namespace AutomationToolkit.App;

/// <summary>アプリケーションのエントリポイント。各コンポーネントの生成と破棄を担当する</summary>
public partial class App : Application {
	/// <summary>単一インスタンス制御に使うミューテックス名</summary>
	private const string SINGLE_INSTANCE_MUTEX_NAME = "AutomationToolkit.SingleInstance.9A1F";

	/// <summary>単一インスタンス制御用のミューテックス</summary>
	private Mutex? singleInstanceMutex;
	/// <summary>グローバル入力フック</summary>
	private LowLevelInputHook? hook;
	/// <summary>グローバルホットキーの管理</summary>
	private HotkeyManager? hotkeys;
	/// <summary>メイン画面のビューモデル</summary>
	private MainViewModel? viewModel;

	/// <summary>各コンポーネントを組み立ててメインウィンドウを表示する</summary>
	/// <param name="e">起動イベントの引数</param>
	protected override void OnStartup(StartupEventArgs e) {
		// 二重フック防止のため単一インスタンスに制限する
		singleInstanceMutex = new Mutex(initiallyOwned: true, SINGLE_INSTANCE_MUTEX_NAME, out var isNew);
		if (isNew == false) {
			MessageBox.Show("AutomationToolkit は既に起動しています。", "AutomationToolkit",
				MessageBoxButton.OK, MessageBoxImage.Information);
			Shutdown();
			return;
		}

		base.OnStartup(e);

		var settingsService = new SettingsService();
		hook = new LowLevelInputHook();
		try {
			hook.Start();
		}
		catch (Exception ex) {
			MessageBox.Show($"入力フックを開始できませんでした。\n{ex.Message}", "起動エラー",
				MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown();
			return;
		}

		hotkeys = new HotkeyManager(hook);
		var recorder = new MacroRecorder(hook);
		var player = new MacroPlayer(new InputSynthesizer());
		viewModel = new MainViewModel(settingsService, hook, recorder, player, hotkeys);

		var window = new MainWindow(viewModel);
		MainWindow = window;
		window.Show();
	}

	/// <summary>各コンポーネントを破棄する</summary>
	/// <param name="e">終了イベントの引数</param>
	protected override void OnExit(ExitEventArgs e) {
		viewModel?.Dispose();
		singleInstanceMutex?.Dispose();
		base.OnExit(e);
	}
}
