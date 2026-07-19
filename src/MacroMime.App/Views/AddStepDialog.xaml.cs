using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MacroMime.App.ViewModels;
using MacroMime.Core.Models;

namespace MacroMime.App.Views;

/// <summary>新規ステップの種別とパラメーターを入力して作成するダイアログ</summary>
public partial class AddStepDialog : Window {
	/// <summary>キー押下メッセージ</summary>
	private const int WM_KEYDOWN = 0x0100;
	/// <summary>システムキー押下メッセージ ( Alt 併用時など )</summary>
	private const int WM_SYSKEYDOWN = 0x0104;
	/// <summary>カーソル位置取得までのカウントダウン秒数</summary>
	private const int CURSOR_PICK_COUNTDOWN_SECONDS = 3;

	/// <summary>入力状態を保持するビューモデル</summary>
	private readonly AddStepViewModel viewModel;
	/// <summary>カーソル位置取得のカウントダウンタイマー</summary>
	private DispatcherTimer? cursorPickTimer;
	/// <summary>カウントダウンの残り秒数</summary>
	private int cursorPickRemainingSeconds;
	/// <summary>カーソル位置取得ボタンの元の表示内容</summary>
	private object? pickCursorButtonContent;

	/// <summary>確定時に作成されたステップ。未確定なら null</summary>
	public MacroStep? createdStep => viewModel.createdStep;

	/// <summary>ビューモデルを受け取って初期化する</summary>
	/// <param name="viewModel">入力状態を保持するビューモデル</param>
	public AddStepDialog(AddStepViewModel viewModel) {
		InitializeComponent();
		this.viewModel = viewModel;
		DataContext = viewModel;
		viewModel.CloseRequested += () => DialogResult = true;
	}

	/// <summary>ウィンドウハンドルの生成後にキーキャプチャ用のメッセージフックを登録する</summary>
	/// <param name="e">イベントの引数</param>
	protected override void OnSourceInitialized(EventArgs e) {
		base.OnSourceInitialized(e);
		HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProcHook);
	}

	/// <summary>キャプチャモード中のキー押下メッセージからキー情報を取り込む</summary>
	/// <remarks>録画時のフックと同じ生値 ( scanCode / 拡張キーフラグ ) を得るため、WPF のキーイベントではなく生メッセージを見る</remarks>
	/// <param name="hwnd">ウィンドウハンドル</param>
	/// <param name="message">メッセージ番号</param>
	/// <param name="wParam">仮想キーコード</param>
	/// <param name="lParam">スキャンコードと拡張キーフラグを含む付加情報</param>
	/// <param name="handled">メッセージを処理済みにするかどうか</param>
	/// <returns>メッセージの処理結果</returns>
	private nint WndProcHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled) {
		// キャプチャモード中だけ Esc / Enter / Tab を含む全キーを飲み込み、ダイアログ操作に化けさせない
		if (viewModel.IsCapturingKey && message is WM_KEYDOWN or WM_SYSKEYDOWN) {
			var virtualKey = (ushort)wParam;
			var scanCode = (ushort)((lParam >> 16) & 0xFF);
			var isExtended = (lParam & (1L << 24)) != 0;
			viewModel.CaptureKey(virtualKey, scanCode, isExtended);
			handled = true;
		}
		return 0;
	}

	/// <summary>カウントダウン後に現在のカーソル位置を座標欄へ取り込む</summary>
	/// <remarks>ボタンを押した瞬間はカーソルがボタン上にあるため、対象位置へ移動する時間を置いてから取得する</remarks>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="e">クリックイベントの引数</param>
	private void PickCursor_Click(object sender, RoutedEventArgs e) {
		if (cursorPickTimer is not null) return;
		pickCursorButtonContent = PickCursorButton.Content;
		cursorPickRemainingSeconds = CURSOR_PICK_COUNTDOWN_SECONDS;
		PickCursorButton.Content = $"{cursorPickRemainingSeconds}…";
		cursorPickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		cursorPickTimer.Tick += OnCursorPickTimerTick;
		cursorPickTimer.Start();
	}

	/// <summary>カウントダウンを進め、0 になったらカーソル位置を取得する</summary>
	/// <param name="sender">イベントの発生元</param>
	/// <param name="e">イベントの引数</param>
	private void OnCursorPickTimerTick(object? sender, EventArgs e) {
		cursorPickRemainingSeconds--;
		if (cursorPickRemainingSeconds > 0) {
			PickCursorButton.Content = $"{cursorPickRemainingSeconds}…";
			return;
		}
		cursorPickTimer?.Stop();
		cursorPickTimer = null;
		PickCursorButton.Content = pickCursorButtonContent;
		if (GetCursorPos(out var point)) {
			viewModel.X = point.x;
			viewModel.Y = point.y;
		}
	}

	/// <summary>スクリーン座標の点</summary>
	[StructLayout(LayoutKind.Sequential)]
	private struct POINT {
		/// <summary>X 座標</summary>
		public int x;
		/// <summary>Y 座標</summary>
		public int y;
	}

	/// <summary>現在のカーソル位置を仮想スクリーンの物理ピクセルで取得する</summary>
	/// <param name="point">取得したカーソル位置</param>
	/// <returns>取得に成功したら true</returns>
	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT point);
}
