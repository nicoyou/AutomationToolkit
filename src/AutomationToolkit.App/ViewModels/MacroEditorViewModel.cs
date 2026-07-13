using System.IO;
using System.Windows;
using AutomationToolkit.Core.Editing;
using AutomationToolkit.Core.Models;
using AutomationToolkit.Core.Persistence;
using AutomationToolkit.Core.Playback;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutomationToolkit.App.ViewModels;

/// <summary>ステップ一覧の 1 行分の表示データ</summary>
/// <param name="index">1 始まりのステップ番号</param>
/// <param name="delayBeforeMs">実行前の待機ミリ秒数</param>
/// <param name="description">ステップの要約文字列</param>
public sealed record MacroStepRow(int index, int delayBeforeMs, string description);

/// <summary>マクロ編集ダイアログのビューモデル</summary>
public sealed partial class MacroEditorViewModel : ObservableObject {
	/// <summary>クリックのクールダウン短縮の既定値 ( ms )</summary>
	private const int DEFAULT_CLICK_COOLDOWN_MS = 50;
	/// <summary>クリック時間の短縮の既定値 ( ms )</summary>
	private const int DEFAULT_CLICK_DURATION_MS = 20;

	/// <summary>マクロファイルのリポジトリ</summary>
	private readonly MacroRepository repository;
	/// <summary>テスト再生に使う再生エンジン</summary>
	private readonly IMacroPlayer player;
	/// <summary>編集対象のマクロファイルのパス</summary>
	private readonly string filePath;
	/// <summary>編集対象のマクロの作業コピー</summary>
	private readonly Macro macro;
	/// <summary>一括操作前のステップ列のスナップショット</summary>
	private readonly Stack<List<MacroStep>> undoStack = new();
	/// <summary>テスト再生を停止するためのキャンセルトークンソース</summary>
	private CancellationTokenSource? testPlaybackCancellation;
	/// <summary>ステップ一覧の表示データ</summary>
	[ObservableProperty]
	private IReadOnlyList<MacroStepRow> stepRows = [];
	/// <summary>テスト再生中かどうか</summary>
	[ObservableProperty]
	private bool isTestPlaying;
	/// <summary>直前の編集操作の結果表示。空なら非表示</summary>
	[ObservableProperty]
	private string operationResultText = string.Empty;
	/// <summary>マクロの推定実行時間の表示</summary>
	[ObservableProperty]
	private string totalDurationText = string.Empty;
	/// <summary>mouseDown の待機時間の統計表示</summary>
	[ObservableProperty]
	private string mouseDownStatisticsText = string.Empty;
	/// <summary>mouseUp の待機時間の統計表示</summary>
	[ObservableProperty]
	private string mouseUpStatisticsText = string.Empty;
	/// <summary>削除した mouseMove の待機時間を次に残るステップへ加算するかどうか</summary>
	[ObservableProperty]
	private bool addRemovedDelayToNextStep = true;
	/// <summary>クールダウン短縮で変換対象とするしきい値 ( ms )</summary>
	[ObservableProperty]
	private int clickCooldownThresholdMs = DEFAULT_CLICK_COOLDOWN_MS;
	/// <summary>クールダウン短縮の変換後の値 ( ms )</summary>
	[ObservableProperty]
	private int clickCooldownNewDelayMs = DEFAULT_CLICK_COOLDOWN_MS;
	/// <summary>クリック時間短縮で変換対象とするしきい値 ( ms )</summary>
	[ObservableProperty]
	private int clickDurationThresholdMs = DEFAULT_CLICK_DURATION_MS;
	/// <summary>クリック時間短縮の変換後の値 ( ms )</summary>
	[ObservableProperty]
	private int clickDurationNewDelayMs = DEFAULT_CLICK_DURATION_MS;

	/// <summary>ダイアログを閉じる要求。引数が true なら保存済み</summary>
	public event Action<bool>? CloseRequested;

	/// <summary>未保存の変更があるかどうか</summary>
	public bool isDirty => undoStack.Count > 0;
	/// <summary>ウィンドウタイトル</summary>
	public string title => $"マクロ編集 - {macro.name}";
	/// <summary>編集操作を実行できるかどうか</summary>
	private bool canEdit => IsTestPlaying == false;
	/// <summary>元に戻す操作を実行できるかどうか</summary>
	private bool canUndo => isDirty && canEdit;

	/// <summary>編集対象のマクロを受け取って表示を初期化する</summary>
	/// <param name="macro">編集対象のマクロの作業コピー</param>
	/// <param name="filePath">編集対象のマクロファイルのパス</param>
	/// <param name="repository">マクロファイルのリポジトリ</param>
	/// <param name="player">テスト再生に使う再生エンジン</param>
	public MacroEditorViewModel(Macro macro, string filePath, MacroRepository repository, IMacroPlayer player) {
		this.macro = macro;
		this.filePath = filePath;
		this.repository = repository;
		this.player = player;
		RefreshView();
	}

	/// <summary>マウスボタン押下中でない区間の mouseMove を全て削除する</summary>
	[RelayCommand(CanExecute = nameof(canEdit))]
	private void RemoveNonDragMouseMoves() {
		var delayHandling = AddRemovedDelayToNextStep ? RemovedDelayHandling.AddToNextStep : RemovedDelayHandling.Discard;
		var removedCount = ApplyEdit(steps => MacroStepEditor.RemoveNonDragMouseMoves(steps, delayHandling));
		OperationResultText = $"不要な mouseMove を {removedCount} 件削除しました";
	}

	/// <summary>しきい値以上の mouseDown の待機時間を指定値へ短縮する</summary>
	[RelayCommand(CanExecute = nameof(canEdit))]
	private void UnifyClickCooldown() {
		if (ValidateDelayInputs(ClickCooldownThresholdMs, ClickCooldownNewDelayMs) == false) return;
		var changedCount = ApplyEdit(steps =>
			MacroStepEditor.UnifyDelays<MouseDownStep>(steps, ClickCooldownThresholdMs, ClickCooldownNewDelayMs));
		OperationResultText = $"mouseDown の待機時間を {changedCount} 件変換しました";
	}

	/// <summary>しきい値以上の mouseUp の待機時間 ( ボタン押下時間 ) を指定値へ短縮する</summary>
	[RelayCommand(CanExecute = nameof(canEdit))]
	private void UnifyClickDuration() {
		if (ValidateDelayInputs(ClickDurationThresholdMs, ClickDurationNewDelayMs) == false) return;
		var changedCount = ApplyEdit(steps =>
			MacroStepEditor.UnifyDelays<MouseUpStep>(steps, ClickDurationThresholdMs, ClickDurationNewDelayMs));
		OperationResultText = $"mouseUp の待機時間を {changedCount} 件変換しました";
	}

	/// <summary>直前の一括操作を取り消す</summary>
	[RelayCommand(CanExecute = nameof(canUndo))]
	private void Undo() {
		if (undoStack.Count == 0) return;
		macro.steps.Clear();
		macro.steps.AddRange(undoStack.Pop());
		OperationResultText = "直前の操作を元に戻しました";
		RefreshView();
	}

	/// <summary>編集中のマクロを保存せずに再生して動作確認する</summary>
	[RelayCommand(CanExecute = nameof(canEdit))]
	private async Task TestPlayAsync() {
		testPlaybackCancellation = new CancellationTokenSource();
		IsTestPlaying = true;
		try {
			await player.PlayAsync(macro, new PlaybackOptions(), testPlaybackCancellation.Token);
		}
		finally {
			testPlaybackCancellation.Dispose();
			testPlaybackCancellation = null;
			IsTestPlaying = false;
		}
	}

	/// <summary>テスト再生を停止する</summary>
	[RelayCommand(CanExecute = nameof(IsTestPlaying))]
	private void StopTestPlayback() => testPlaybackCancellation?.Cancel();

	/// <summary>全ての変更をマクロファイルへ上書き保存して閉じる</summary>
	[RelayCommand(CanExecute = nameof(canEdit))]
	private void SaveAndClose() {
		try {
			repository.SaveTo(filePath, macro);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
			MessageBox.Show(ex.Message, "保存できません", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}
		CloseRequested?.Invoke(true);
	}

	/// <summary>全ての変更を破棄して閉じる</summary>
	[RelayCommand(CanExecute = nameof(canEdit))]
	private void DiscardAndClose() => CloseRequested?.Invoke(false);

	/// <summary>スナップショットを積んでから一括操作を実行する。変更が無ければスナップショットを捨てる</summary>
	/// <param name="edit">ステップ列への一括操作。変更した件数を返す</param>
	/// <returns>変更した件数</returns>
	private int ApplyEdit(Func<List<MacroStep>, int> edit) {
		undoStack.Push(MacroCloner.CloneSteps(macro.steps));
		var changedCount = edit(macro.steps);
		if (changedCount == 0) undoStack.Pop();
		RefreshView();
		return changedCount;
	}

	/// <summary>しきい値と変換後の値が有効か検証し、不正ならエラーを表示する</summary>
	/// <param name="thresholdMs">変換対象とするしきい値 ( ms )</param>
	/// <param name="newDelayMs">変換後の値 ( ms )</param>
	/// <returns>両方とも有効なら true</returns>
	private bool ValidateDelayInputs(int thresholdMs, int newDelayMs) {
		if (thresholdMs >= 0 && newDelayMs >= 0) return true;
		OperationResultText = "しきい値と変換後の値には 0 以上を指定してください";
		return false;
	}

	/// <summary>ステップ一覧・統計・変更状態の表示を現在のステップ列から作り直す</summary>
	private void RefreshView() {
		StepRows = macro.steps
			.Select((step, index) => new MacroStepRow(index + 1, step.delayBeforeMs, MacroStepFormatter.Describe(step)))
			.ToList();
		MouseDownStatisticsText = FormatStatistics(MacroStepEditor.CalculateDelayStatistics<MouseDownStep>(macro.steps));
		MouseUpStatisticsText = FormatStatistics(MacroStepEditor.CalculateDelayStatistics<MouseUpStep>(macro.steps));
		TotalDurationText = $"実行時間: {FormatDuration(MacroStepEditor.CalculateTotalDurationMs(macro.steps))}";
		OnPropertyChanged(nameof(isDirty));
		UndoCommand.NotifyCanExecuteChanged();
	}

	/// <summary>待機時間の統計を表示用文字列へ整形する</summary>
	/// <param name="statistics">待機時間の統計。null なら対象なし</param>
	/// <returns>統計の表示用文字列</returns>
	private static string FormatStatistics(DelayStatistics? statistics) {
		if (statistics is null) return "対象なし";
		return $"{statistics.count} 件 | 最小 {statistics.minimumMs} / 中央 {statistics.medianMs:0.#} / 最大 {statistics.maximumMs} ms";
	}

	/// <summary>実行時間を表示用文字列へ整形する</summary>
	/// <param name="totalMs">実行時間 ( ms )</param>
	/// <returns>実行時間の表示用文字列</returns>
	private static string FormatDuration(long totalMs) {
		var totalSeconds = totalMs / 1000.0;
		if (totalSeconds < 60) return $"{totalSeconds:0.0} 秒";
		var minutes = (long)(totalSeconds / 60);
		return $"{minutes} 分 {totalSeconds - minutes * 60:0.0} 秒";
	}

	/// <summary>テスト再生状態の変化に応じて各コマンドの実行可否を更新する</summary>
	/// <param name="value">変更後のテスト再生状態</param>
	partial void OnIsTestPlayingChanged(bool value) {
		RemoveNonDragMouseMovesCommand.NotifyCanExecuteChanged();
		UnifyClickCooldownCommand.NotifyCanExecuteChanged();
		UnifyClickDurationCommand.NotifyCanExecuteChanged();
		UndoCommand.NotifyCanExecuteChanged();
		TestPlayCommand.NotifyCanExecuteChanged();
		StopTestPlaybackCommand.NotifyCanExecuteChanged();
		SaveAndCloseCommand.NotifyCanExecuteChanged();
		DiscardAndCloseCommand.NotifyCanExecuteChanged();
	}
}
