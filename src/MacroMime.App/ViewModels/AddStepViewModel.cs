using MacroMime.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MacroMime.App.ViewModels;

/// <summary>追加するステップの種別</summary>
public enum AddStepKind {
	/// <summary>キーを押す</summary>
	KeyDown,
	/// <summary>キーを離す</summary>
	KeyUp,
	/// <summary>マウスボタンを押す</summary>
	MouseDown,
	/// <summary>マウスボタンを離す</summary>
	MouseUp,
	/// <summary>カーソルを移動する</summary>
	MouseMove,
	/// <summary>ホイールを回転する</summary>
	MouseWheel,
}

/// <summary>ステップ種別の選択肢の表示項目</summary>
/// <param name="kind">ステップの種別</param>
/// <param name="label">表示名</param>
public sealed record AddStepKindItem(AddStepKind kind, string label);

/// <summary>ステップ追加ダイアログのビューモデル</summary>
public sealed partial class AddStepViewModel : ObservableObject {
	/// <summary>選択中のステップ種別</summary>
	[ObservableProperty]
	private AddStepKind selectedKind = AddStepKind.MouseDown;
	/// <summary>実行前の待機ミリ秒数</summary>
	[ObservableProperty]
	private int delayBeforeMs;
	/// <summary>カーソルの X 座標</summary>
	[ObservableProperty]
	private int x;
	/// <summary>カーソルの Y 座標</summary>
	[ObservableProperty]
	private int y;
	/// <summary>選択中のマウスボタン</summary>
	[ObservableProperty]
	private MouseButton selectedButton = MouseButton.Left;
	/// <summary>ホイールの回転量。1 ノッチは ±120</summary>
	[ObservableProperty]
	private int wheelDelta = 120;
	/// <summary>水平ホイールかどうか</summary>
	[ObservableProperty]
	private bool isHorizontalWheel;
	/// <summary>キャプチャしたキーの仮想キーコード。0 なら未取得</summary>
	[ObservableProperty]
	private ushort capturedVirtualKey;
	/// <summary>キャプチャしたキーのスキャンコード</summary>
	[ObservableProperty]
	private ushort capturedScanCode;
	/// <summary>キャプチャしたキーが拡張キーかどうか</summary>
	[ObservableProperty]
	private bool capturedIsExtended;
	/// <summary>キーキャプチャモード中かどうか</summary>
	[ObservableProperty]
	private bool isCapturingKey;

	/// <summary>入力内容の確定によるダイアログを閉じる要求</summary>
	public event Action? CloseRequested;

	/// <summary>確定時に作成されたステップ。未確定なら null</summary>
	public MacroStep? createdStep { get; private set; }
	/// <summary>ステップ種別の選択肢</summary>
	public IReadOnlyList<AddStepKindItem> kinds { get; } = [
		new(AddStepKind.KeyDown, "keyDown ( キーを押す )"),
		new(AddStepKind.KeyUp, "keyUp ( キーを離す )"),
		new(AddStepKind.MouseDown, "mouseDown ( ボタンを押す )"),
		new(AddStepKind.MouseUp, "mouseUp ( ボタンを離す )"),
		new(AddStepKind.MouseMove, "mouseMove ( カーソル移動 )"),
		new(AddStepKind.MouseWheel, "mouseWheel ( ホイール回転 )"),
	];
	/// <summary>マウスボタンの選択肢</summary>
	public IReadOnlyList<MouseButton> mouseButtons { get; } = Enum.GetValues<MouseButton>();

	/// <summary>キー入力欄を表示するかどうか</summary>
	public bool showsKeyInput => SelectedKind is AddStepKind.KeyDown or AddStepKind.KeyUp;
	/// <summary>座標入力欄を表示するかどうか</summary>
	public bool showsCoordinates => showsKeyInput == false;
	/// <summary>マウスボタン選択欄を表示するかどうか</summary>
	public bool showsMouseButton => SelectedKind is AddStepKind.MouseDown or AddStepKind.MouseUp;
	/// <summary>ホイール入力欄を表示するかどうか</summary>
	public bool showsWheel => SelectedKind == AddStepKind.MouseWheel;
	/// <summary>キャプチャしたキーの表示名</summary>
	public string capturedKeyText => CapturedVirtualKey == 0 ? "( 未取得 )" : VirtualKeyNames.GetName(CapturedVirtualKey);
	/// <summary>入力内容を確定できるかどうか</summary>
	private bool canConfirm =>
		DelayBeforeMs >= 0
		&& (showsKeyInput == false || CapturedVirtualKey != 0)
		&& (showsWheel == false || WheelDelta != 0);

	/// <summary>押されたキーの情報を取り込んでキャプチャモードを終了する</summary>
	/// <param name="virtualKey">仮想キーコード</param>
	/// <param name="scanCode">ハードウェアスキャンコード</param>
	/// <param name="isExtended">拡張キーかどうか</param>
	public void CaptureKey(ushort virtualKey, ushort scanCode, bool isExtended) {
		CapturedVirtualKey = virtualKey;
		CapturedScanCode = scanCode;
		CapturedIsExtended = isExtended;
		IsCapturingKey = false;
	}

	/// <summary>入力内容からステップを作成してダイアログを閉じる</summary>
	[RelayCommand(CanExecute = nameof(canConfirm))]
	private void Confirm() {
		createdStep = BuildStep();
		CloseRequested?.Invoke();
	}

	/// <summary>現在の入力内容からステップを組み立てる</summary>
	/// <returns>組み立てたステップ</returns>
	private MacroStep BuildStep() => SelectedKind switch {
		AddStepKind.KeyDown => new KeyDownStep { virtualKey = CapturedVirtualKey, scanCode = CapturedScanCode, isExtended = CapturedIsExtended, delayBeforeMs = DelayBeforeMs },
		AddStepKind.KeyUp => new KeyUpStep { virtualKey = CapturedVirtualKey, scanCode = CapturedScanCode, isExtended = CapturedIsExtended, delayBeforeMs = DelayBeforeMs },
		AddStepKind.MouseDown => new MouseDownStep { button = SelectedButton, x = X, y = Y, delayBeforeMs = DelayBeforeMs },
		AddStepKind.MouseUp => new MouseUpStep { button = SelectedButton, x = X, y = Y, delayBeforeMs = DelayBeforeMs },
		AddStepKind.MouseMove => new MouseMoveStep { x = X, y = Y, delayBeforeMs = DelayBeforeMs },
		AddStepKind.MouseWheel => new MouseWheelStep { delta = WheelDelta, isHorizontal = IsHorizontalWheel, x = X, y = Y, delayBeforeMs = DelayBeforeMs },
		_ => throw new ArgumentException("未定義のステップ種別が指定されました"),
	};

	/// <summary>種別の変化に応じて入力欄の表示と確定可否を更新する</summary>
	/// <param name="value">変更後のステップ種別</param>
	partial void OnSelectedKindChanged(AddStepKind value) {
		OnPropertyChanged(nameof(showsKeyInput));
		OnPropertyChanged(nameof(showsCoordinates));
		OnPropertyChanged(nameof(showsMouseButton));
		OnPropertyChanged(nameof(showsWheel));
		ConfirmCommand.NotifyCanExecuteChanged();
	}

	/// <summary>待機時間の変化に応じて確定可否を更新する</summary>
	/// <param name="value">変更後の待機ミリ秒数</param>
	partial void OnDelayBeforeMsChanged(int value) => ConfirmCommand.NotifyCanExecuteChanged();

	/// <summary>キャプチャしたキーの変化に応じて表示と確定可否を更新する</summary>
	/// <param name="value">変更後の仮想キーコード</param>
	partial void OnCapturedVirtualKeyChanged(ushort value) {
		OnPropertyChanged(nameof(capturedKeyText));
		ConfirmCommand.NotifyCanExecuteChanged();
	}

	/// <summary>ホイール回転量の変化に応じて確定可否を更新する</summary>
	/// <param name="value">変更後の回転量</param>
	partial void OnWheelDeltaChanged(int value) => ConfirmCommand.NotifyCanExecuteChanged();
}
