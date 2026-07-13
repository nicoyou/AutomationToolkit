using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AutomationToolkit.App.Services;
using AutomationToolkit.Core.Hooks;
using AutomationToolkit.Core.Models;
using AutomationToolkit.Core.Persistence;
using AutomationToolkit.Core.Playback;
using AutomationToolkit.Core.Recording;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutomationToolkit.App.ViewModels;

/// <summary>アプリの動作状態</summary>
public enum AppState {
	/// <summary>待機中</summary>
	Idle,
	/// <summary>録画中</summary>
	Recording,
	/// <summary>再生中</summary>
	Playing,
}

/// <summary>メイン画面のビューモデル。録画・再生・ホットキー・設定を統括する</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable {
	/// <summary>設定の読み書きサービス</summary>
	private readonly SettingsService settingsService;
	/// <summary>録画エンジン</summary>
	private readonly IMacroRecorder recorder;
	/// <summary>再生エンジン</summary>
	private readonly IMacroPlayer player;
	/// <summary>グローバルホットキーの管理</summary>
	private readonly IHotkeyManager hotkeys;
	/// <summary>グローバル入力フック。Dispose のために保持する</summary>
	private readonly LowLevelInputHook hook;
	/// <summary>現在の設定</summary>
	private AppSettings settings;
	/// <summary>マクロファイルのリポジトリ</summary>
	private MacroRepository repository;
	/// <summary>再生中のマクロを停止するためのキャンセルトークンソース</summary>
	private CancellationTokenSource? playbackCancellationTokenSource;
	/// <summary>マクロファイル名からホットキー登録 ID への対応表</summary>
	private readonly Dictionary<string, Guid> macroHotkeyIds = new(StringComparer.OrdinalIgnoreCase);
	/// <summary>録画トグルホットキーの登録 ID</summary>
	private Guid recordHotkeyId;
	/// <summary>停止ホットキーの登録 ID</summary>
	private Guid stopHotkeyId;
	/// <summary>一覧で選択中のマクロ。null なら未選択</summary>
	[ObservableProperty]
	private MacroItemViewModel? selectedMacro;
	/// <summary>アプリの動作状態</summary>
	[ObservableProperty]
	private AppState state = AppState.Idle;
	/// <summary>ステータスバーに表示する文言</summary>
	[ObservableProperty]
	private string statusText = "待機中";
	/// <summary>録画中に記録したステップ数</summary>
	[ObservableProperty]
	private int recordedStepCount;
	/// <summary>画面に表示するマクロの一覧</summary>
	public ObservableCollection<MacroItemViewModel> macros { get; } = [];

	/// <summary>マクロファイルを保存するフォルダの絶対パス</summary>
	public string macrosFolder => repository.macrosFolder;
	/// <summary>待機中かどうか</summary>
	private bool isIdle => State == AppState.Idle;
	/// <summary>録画中かどうか</summary>
	private bool isRecording => State == AppState.Recording;
	/// <summary>再生を開始できるかどうか</summary>
	private bool canPlay => State == AppState.Idle && SelectedMacro is not null;
	/// <summary>再生中かどうか</summary>
	private bool isPlaying => State == AppState.Playing;

	/// <summary>各コンポーネントを接続し、設定の読み込みとホットキー登録を行う</summary>
	/// <param name="settingsService">設定の読み書きサービス</param>
	/// <param name="hook">グローバル入力フック</param>
	/// <param name="recorder">録画エンジン</param>
	/// <param name="player">再生エンジン</param>
	/// <param name="hotkeys">グローバルホットキーの管理</param>
	public MainViewModel(
		SettingsService settingsService,
		LowLevelInputHook hook,
		IMacroRecorder recorder,
		IMacroPlayer player,
		IHotkeyManager hotkeys) {
		this.settingsService = settingsService;
		this.hook = hook;
		this.recorder = recorder;
		this.player = player;
		this.hotkeys = hotkeys;

		settings = this.settingsService.Load();
		repository = new MacroRepository(settings.macrosFolder ?? this.settingsService.defaultMacrosFolder);

		this.recorder.StepRecorded += (_, count) => Dispatch(() => RecordedStepCount = count);
		this.recorder.IsRecordingChanged += (_, recording) => Dispatch(() =>
			State = recording ? AppState.Recording : AppState.Idle);
		this.player.IsPlayingChanged += (_, playing) => Dispatch(() =>
			State = playing ? AppState.Playing : AppState.Idle);

		RegisterGlobalHotkeys();
		RefreshMacros();
	}

	/// <summary>ウィンドウタイトルなど表示更新のために状態変化を通知するイベント</summary>
	public event Action? StateChangedForTray;

	/// <summary>マクロフォルダを読み直して一覧とホットキーを更新する</summary>
	[RelayCommand]
	private void RefreshMacros() {
		Directory.CreateDirectory(repository.macrosFolder);
		macros.Clear();
		foreach (var path in repository.ListMacroFiles()) {
			var fileName = Path.GetFileName(path);
			var binding = settings.bindings.TryGetValue(fileName, out var existingBinding) ? existingBinding : new MacroBinding();
			string name;
			try {
				name = repository.Load(path).name;
			}
			catch (MacroFormatException) {
				name = $"[破損] {Path.GetFileNameWithoutExtension(path)}";
			}
			macros.Add(new MacroItemViewModel(path, name, binding));
		}
		RebindMacroHotkeys();
	}

	/// <summary>録画を開始する</summary>
	[RelayCommand(CanExecute = nameof(isIdle))]
	private void Record() {
		RecordedStepCount = 0;
		recorder.Start(new RecordingOptions());
	}

	/// <summary>録画を停止し、名前を入力させて保存する</summary>
	[RelayCommand(CanExecute = nameof(isRecording))]
	private void StopRecording() {
		var macro = recorder.Stop();
		var name = PromptForName();
		if (name is null) return; // 保存キャンセル
		macro.name = name;
		repository.Save(macro);
		RefreshMacros();
	}

	/// <summary>選択中のマクロを再生する</summary>
	[RelayCommand(CanExecute = nameof(canPlay))]
	private void Play() => PlaySelected();

	/// <summary>待機中なら選択中のマクロを再生する</summary>
	private void PlaySelected() {
		if (SelectedMacro is null || State != AppState.Idle) return;
		PlayMacro(SelectedMacro);
	}

	/// <summary>マクロを読み込んで再生を開始する</summary>
	/// <param name="item">再生するマクロの行</param>
	private void PlayMacro(MacroItemViewModel item) {
		Macro macro;
		try {
			macro = repository.Load(item.filePath);
		}
		catch (MacroFormatException ex) {
			MessageBox.Show(ex.Message, "再生できません", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		playbackCancellationTokenSource = new CancellationTokenSource();
		var options = new PlaybackOptions { speedMultiplier = item.Speed, loopCount = item.LoopCount };
		_ = player.PlayAsync(macro, options, playbackCancellationTokenSource.Token);
	}

	/// <summary>再生を停止する</summary>
	[RelayCommand(CanExecute = nameof(isPlaying))]
	private void StopPlayback() => playbackCancellationTokenSource?.Cancel();

	/// <summary>ホットキー選択ダイアログを開いてマクロにホットキーを割り当てる</summary>
	/// <param name="item">割り当て対象のマクロの行。null なら選択中のマクロ</param>
	[RelayCommand]
	private void AssignHotkey(MacroItemViewModel? item) {
		item ??= SelectedMacro;
		if (item is null) return;
		var dialog = new Views.HotkeyPickerDialog(item.Hotkey) {
			Owner = Application.Current.MainWindow,
		};
		if (dialog.ShowDialog() == true) {
			item.Hotkey = dialog.chord;
			PersistBindings();
		}
	}

	/// <summary>マクロフォルダをエクスプローラーで開く</summary>
	[RelayCommand]
	private void OpenMacrosFolder() {
		Directory.CreateDirectory(repository.macrosFolder);
		System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
			FileName = repository.macrosFolder,
			UseShellExecute = true,
		});
	}

	/// <summary>全マクロのバインディングを設定ファイルへ保存し、ホットキーを再登録する</summary>
	public void PersistBindings() {
		settings.bindings.Clear();
		foreach (var macroItem in macros) {
			settings.bindings[macroItem.fileName] = macroItem.ToBinding();
		}
		settingsService.Save(settings);
		RebindMacroHotkeys();
	}

	/// <summary>録画トグル・停止のグローバルホットキーを登録する</summary>
	private void RegisterGlobalHotkeys() {
		recordHotkeyId = hotkeys.Register(settings.recordHotkey, () => Dispatch(ToggleRecordFromHotkey));
		stopHotkeyId = hotkeys.Register(settings.stopHotkey, () => Dispatch(StopEverything));
	}

	/// <summary>録画ホットキーで録画の開始・停止をトグルする</summary>
	private void ToggleRecordFromHotkey() {
		if (State == AppState.Recording) {
			StopRecordingCommand.Execute(null);
		}
		else if (State == AppState.Idle) {
			RecordCommand.Execute(null);
		}
	}

	/// <summary>停止ホットキーで再生を停止する</summary>
	private void StopEverything() {
		playbackCancellationTokenSource?.Cancel();
	}

	/// <summary>マクロごとの再生ホットキーを登録し直す</summary>
	private void RebindMacroHotkeys() {
		foreach (var id in macroHotkeyIds.Values) {
			hotkeys.Unregister(id);
		}
		macroHotkeyIds.Clear();

		foreach (var macroItem in macros) {
			if (macroItem.Hotkey is { } chord) {
				var item = macroItem;
				macroHotkeyIds[macroItem.fileName] = hotkeys.Register(chord, () => Dispatch(() => {
					if (State == AppState.Idle) {
						PlayMacro(item);
					}
				}));
			}
		}
	}

	/// <summary>名前入力ダイアログを表示してマクロ名を取得する</summary>
	/// <returns>入力された名前。キャンセルされたら null</returns>
	private string? PromptForName() {
		var dialog = new Views.NameInputDialog {
			Owner = Application.Current.MainWindow,
		};
		return dialog.ShowDialog() == true ? dialog.enteredName : null;
	}

	/// <summary>UI スレッド上でアクションを実行する</summary>
	/// <param name="action">実行するアクション</param>
	private static void Dispatch(Action action) {
		var app = Application.Current;
		if (app is null) {
			action();
		}
		else {
			app.Dispatcher.Invoke(action);
		}
	}

	/// <summary>再生を止め、ホットキーと入力フックを破棄する</summary>
	public void Dispose() {
		playbackCancellationTokenSource?.Cancel();
		(hotkeys as IDisposable)?.Dispose();
		hook.Dispose();
	}

	/// <summary>状態変化に応じてステータス文言とコマンドの実行可否を更新する</summary>
	/// <param name="value">変更後の状態</param>
	partial void OnStateChanged(AppState value) {
		StatusText = value switch {
			AppState.Recording => "録画中...",
			AppState.Playing => "再生中...",
			_ => "待機中",
		};
		RecordCommand.NotifyCanExecuteChanged();
		StopRecordingCommand.NotifyCanExecuteChanged();
		PlayCommand.NotifyCanExecuteChanged();
		StopPlaybackCommand.NotifyCanExecuteChanged();
		StateChangedForTray?.Invoke();
	}

	/// <summary>選択変更時に再生コマンドの実行可否を更新する</summary>
	/// <param name="value">変更後の選択マクロ</param>
	partial void OnSelectedMacroChanged(MacroItemViewModel? value) => PlayCommand.NotifyCanExecuteChanged();
}
