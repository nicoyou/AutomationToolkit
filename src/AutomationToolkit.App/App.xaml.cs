using System.Windows;
using AutomationToolkit.App.Services;
using AutomationToolkit.App.ViewModels;
using AutomationToolkit.App.Views;
using AutomationToolkit.Core.Hooks;
using AutomationToolkit.Core.Playback;
using AutomationToolkit.Core.Recording;

namespace AutomationToolkit.App;

/// <summary>アプリケーションのエントリポイント。各コンポーネントの生成と破棄を担当する</summary>
public partial class App : Application
{
    /// <summary>単一インスタンス制御に使うミューテックス名</summary>
    private const string SingleInstanceMutexName = "AutomationToolkit.SingleInstance.9A1F";

    /// <summary>単一インスタンス制御用のミューテックス</summary>
    private Mutex? _singleInstanceMutex;

    /// <summary>グローバル入力フック</summary>
    private LowLevelInputHook? _hook;

    /// <summary>グローバルホットキーの管理</summary>
    private HotkeyManager? _hotkeys;

    /// <summary>メイン画面のビューモデル</summary>
    private MainViewModel? _viewModel;

    /// <summary>各コンポーネントを組み立ててメインウィンドウを表示する</summary>
    /// <param name="e">起動イベントの引数</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 二重フック防止のため単一インスタンスに制限する
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isNew);
        if (!isNew)
        {
            MessageBox.Show("AutomationToolkit は既に起動しています。", "AutomationToolkit",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var settingsService = new SettingsService();
        _hook = new LowLevelInputHook();
        try
        {
            _hook.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"入力フックを開始できませんでした。\n{ex.Message}", "起動エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _hotkeys = new HotkeyManager(_hook);
        var recorder = new MacroRecorder(_hook);
        var player = new MacroPlayer(new InputSynthesizer());
        _viewModel = new MainViewModel(settingsService, _hook, recorder, player, _hotkeys);

        var window = new MainWindow(_viewModel);
        MainWindow = window;
        window.Show();
    }

    /// <summary>各コンポーネントを破棄する</summary>
    /// <param name="e">終了イベントの引数</param>
    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
