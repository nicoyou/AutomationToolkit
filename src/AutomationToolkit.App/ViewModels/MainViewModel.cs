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
	/// <summary>表示中のマクロ編集ダイアログのビューモデル。停止ホットキーの連携に使う</summary>
	private MacroEditorViewModel? activeEditor;
	/// <summary>再生中のマクロの steps 各要素の JSON 行番号。行番号不明なら空</summary>
	private IReadOnlyList<int> playingStepLines = [];
	/// <summary>UI 反映待ちの最新の再生進行状況</summary>
	private PlaybackProgress? pendingProgress;
	/// <summary>進行表示の UI 更新をキュー済みなら 1</summary>
	private int progressUpdateQueued;
	/// <summary>マクロファイル名からホットキー登録 ID への対応表</summary>
	private readonly Dictionary<string, Guid> macroHotkeyIds = new(StringComparer.OrdinalIgnoreCase);
	/// <summary>録画トグルホットキーの登録 ID</summary>
	private Guid recordHotkeyId;
	/// <summary>停止ホットキーの登録 ID</summary>
	private Guid stopHotkeyId;
	/// <summary>マクロ名の変更を反映中かどうか。差し戻しによる再入を防ぐ</summary>
	private bool isApplyingMacroRename;
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
	/// <summary>再生の進行状況の表示文言。空なら非表示</summary>
	/// <remarks>停止後も次の再生・録画開始まで最終実行位置として保持する</remarks>
	[ObservableProperty]
	private string playbackProgressText = string.Empty;
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
		this.player.ProgressChanged += OnPlaybackProgress;

		RegisterGlobalHotkeys();
		RefreshMacros();
	}

	/// <summary>ウィンドウタイトルなど表示更新のために状態変化を通知するイベント</summary>
	public event Action? StateChangedForTray;

	/// <summary>マクロフォルダを読み直して一覧とホットキーを更新する</summary>
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
			var macroItem = new MacroItemViewModel(path, name, binding);
			macroItem.NameEdited += OnMacroNameEdited;
			macros.Add(macroItem);
		}
		RebindMacroHotkeys();
	}

	/// <summary>録画を開始する</summary>
	[RelayCommand(CanExecute = nameof(isIdle))]
	private void Record() {
		RecordedStepCount = 0;
		PlaybackProgressText = string.Empty;
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
		LoadedMacro loaded;
		try {
			loaded = repository.LoadWithStepLines(item.filePath);
		}
		catch (MacroFormatException ex) {
			MessageBox.Show(ex.Message, "再生できません", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		playingStepLines = loaded.stepLines;
		PlaybackProgressText = string.Empty;
		playbackCancellationTokenSource = new CancellationTokenSource();
		var options = new PlaybackOptions { speedMultiplier = item.Speed, loopCount = item.LoopCount };
		_ = player.PlayAsync(loaded.macro, options, playbackCancellationTokenSource.Token);
	}

	/// <summary>再生を停止する</summary>
	[RelayCommand(CanExecute = nameof(isPlaying))]
	private void StopPlayback() => playbackCancellationTokenSource?.Cancel();

	/// <summary>選択中のマクロを編集ダイアログで開く</summary>
	[RelayCommand(CanExecute = nameof(canPlay))]
	private void EditMacro() {
		if (SelectedMacro is null) return;
		Macro macro;
		try {
			macro = repository.Load(SelectedMacro.filePath);
		}
		catch (MacroFormatException ex) {
			MessageBox.Show(ex.Message, "編集できません", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}
		var editorViewModel = new MacroEditorViewModel(macro, SelectedMacro.filePath, repository, player);
		activeEditor = editorViewModel;
		try {
			new Views.MacroEditorDialog(editorViewModel) {
				Owner = Application.Current.MainWindow,
			}.ShowDialog();
		}
		finally {
			activeEditor = null;
		}
	}

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

	/// <summary>確認の上で対象マクロのファイルをゴミ箱へ移動し、一覧と設定を更新する</summary>
	/// <param name="item">削除対象のマクロの行。null なら選択中のマクロ</param>
	[RelayCommand(CanExecute = nameof(isIdle))]
	private void DeleteMacro(MacroItemViewModel? item) {
		item ??= SelectedMacro;
		if (item is null) return;
		var confirmation = MessageBox.Show(
			$"「{item.Name}」をゴミ箱へ移動しますか?", "マクロの削除",
			MessageBoxButton.YesNo, MessageBoxImage.Question);
		if (confirmation != MessageBoxResult.Yes) return;
		try {
			repository.Delete(item.filePath);
		}
		catch (Exception ex) when (ex is IOException or FileNotFoundException) {
			MessageBox.Show(ex.Message, "削除できません", MessageBoxButton.OK, MessageBoxImage.Warning);
		}
		RefreshMacros();
		PersistBindings();
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
		activeEditor?.StopTestPlaybackCommand.Execute(null);
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

	/// <summary>保留中の最新の進行状況を表示文言へ反映する</summary>
	/// <remarks>キュー済みフラグを先に解除してから最新値を読むことで、直後の通知を取りこぼさない</remarks>
	private void ApplyPendingProgress() {
		Volatile.Write(ref progressUpdateQueued, 0);
		var latest = Volatile.Read(ref pendingProgress);
		if (latest is not null) PlaybackProgressText = FormatProgress(latest);
	}

	/// <summary>進行状況からステータスバー用の表示文言を組み立てる</summary>
	/// <param name="progress">再生の進行状況</param>
	/// <returns>ステータスバー用の表示文言</returns>
	private string FormatProgress(PlaybackProgress progress) {
		var loopPart = progress.loopCount == 1 ? string.Empty : $" ( {progress.loopIndex + 1} 周目 )";
		var linePart = progress.stepIndex < playingStepLines.Count
			? $"行 {playingStepLines[progress.stepIndex]}: " : string.Empty;
		return $"ステップ {progress.stepIndex + 1}/{progress.totalSteps}{loopPart} | {linePart}{MacroStepFormatter.Describe(progress.step)}";
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
		EditMacroCommand.NotifyCanExecuteChanged();
		DeleteMacroCommand.NotifyCanExecuteChanged();
		StateChangedForTray?.Invoke();
	}

	/// <summary>選択変更時に再生・編集コマンドの実行可否を更新する</summary>
	/// <param name="value">変更後の選択マクロ</param>
	partial void OnSelectedMacroChanged(MacroItemViewModel? value) {
		PlayCommand.NotifyCanExecuteChanged();
		EditMacroCommand.NotifyCanExecuteChanged();
	}

	/// <summary>再生スレッドからの進行通知を最新値だけ保持し、UI スレッドへ合流して反映する</summary>
	/// <remarks>UI 更新のキューは常に高々 1 件になるため、高速再生でも再生スレッドをブロックしない</remarks>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="progress">再生の進行状況</param>
	private void OnPlaybackProgress(object? sender, PlaybackProgress progress) {
		Volatile.Write(ref pendingProgress, progress);
		if (Interlocked.Exchange(ref progressUpdateQueued, 1) != 0) return;
		var app = Application.Current;
		if (app is null) {
			ApplyPendingProgress();
		}
		else {
			app.Dispatcher.BeginInvoke(ApplyPendingProgress);
		}
	}

	/// <summary>マクロ名の編集を受けてファイルへ反映する。失敗時は元の名前に差し戻す</summary>
	/// <param name="item">名前が編集されたマクロの行</param>
	/// <param name="oldName">変更前の名前</param>
	private void OnMacroNameEdited(MacroItemViewModel item, string oldName) {
		if (isApplyingMacroRename) return;
		isApplyingMacroRename = true;
		try {
			var newName = item.Name.Trim();
			if (string.IsNullOrEmpty(newName) || newName == oldName) {
				item.Name = oldName;
				return;
			}
			try {
				repository.Rename(item.filePath, newName);
				item.Name = newName;
			}
			catch (Exception ex) when (ex is MacroFormatException or IOException) {
				MessageBox.Show(ex.Message, "名前を変更できません", MessageBoxButton.OK, MessageBoxImage.Warning);
				item.Name = oldName;
			}
		}
		finally {
			isApplyingMacroRename = false;
		}
	}
}
