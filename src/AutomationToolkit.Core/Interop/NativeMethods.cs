using System.Runtime.InteropServices;

namespace AutomationToolkit.Core.Interop;

/// <summary>SetWindowsHookEx に渡すフックプロシージャ</summary>
/// <param name="nCode">フックコード</param>
/// <param name="wParam">メッセージ種別に依存する値</param>
/// <param name="lParam">メッセージ種別に依存する値</param>
/// <returns>後続フックの戻り値、または入力を握りつぶす場合は 1</returns>
internal delegate nint HookProc(int nCode, nint wParam, nint lParam);

/// <summary>Win32 API の P/Invoke 宣言</summary>
internal static class NativeMethods
{
    /// <summary>合成した入力イベントを入力ストリームへ送出する</summary>
    /// <param name="cInputs">送出する入力の件数</param>
    /// <param name="pInputs">送出する入力の配列</param>
    /// <param name="cbSize">INPUT 構造体のサイズ</param>
    /// <returns>実際に送出できた件数</returns>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    /// <summary>システム情報を取得する</summary>
    /// <param name="nIndex">取得する情報を示す SM_* 値</param>
    /// <returns>要求したシステム情報の値</returns>
    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    /// <summary>仮想キーコードとスキャンコードを相互変換する</summary>
    /// <param name="uCode">変換元のキーコード</param>
    /// <param name="uMapType">変換方法を示す MAPVK_* 値</param>
    /// <returns>変換後のキーコード</returns>
    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

    /// <summary>キーの現在の押下状態を取得する</summary>
    /// <param name="vKey">仮想キーコード</param>
    /// <returns>最上位ビットが 1 なら押下中</returns>
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    /// <summary>システムタイマーの分解能を引き上げる</summary>
    /// <param name="uPeriod">要求する分解能のミリ秒数</param>
    /// <returns>成功なら 0</returns>
    [DllImport("winmm.dll")]
    internal static extern uint timeBeginPeriod(uint uPeriod);

    /// <summary>timeBeginPeriod で引き上げた分解能を元に戻す</summary>
    /// <param name="uPeriod">timeBeginPeriod に渡した値と同じ値</param>
    /// <returns>成功なら 0</returns>
    [DllImport("winmm.dll")]
    internal static extern uint timeEndPeriod(uint uPeriod);

    /// <summary>フックプロシージャをフックチェーンに登録する</summary>
    /// <param name="idHook">フック種別を示す WH_* 値</param>
    /// <param name="lpfn">フックプロシージャ</param>
    /// <param name="hMod">フックプロシージャを含むモジュールのハンドル</param>
    /// <param name="dwThreadId">対象スレッド ID。0 で全スレッド</param>
    /// <returns>フックのハンドル。失敗なら 0</returns>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    /// <summary>フックチェーンからフックを解除する</summary>
    /// <param name="hhk">解除するフックのハンドル</param>
    /// <returns>成功したかどうか</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hhk);

    /// <summary>フックチェーンの次のフックへ処理を渡す</summary>
    /// <param name="hhk">現在のフックのハンドル</param>
    /// <param name="nCode">フックプロシージャが受け取ったフックコード</param>
    /// <param name="wParam">フックプロシージャが受け取った値</param>
    /// <param name="lParam">フックプロシージャが受け取った値</param>
    /// <returns>次のフックの戻り値</returns>
    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    /// <summary>モジュールのハンドルを取得する</summary>
    /// <param name="lpModuleName">モジュール名。null で自プロセスの実行ファイル</param>
    /// <returns>モジュールのハンドル</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? lpModuleName);

    /// <summary>スレッドのメッセージキューからメッセージを取り出す</summary>
    /// <param name="lpMsg">取り出したメッセージの格納先</param>
    /// <param name="hWnd">対象ウィンドウのハンドル。0 で全ウィンドウ</param>
    /// <param name="wMsgFilterMin">取得するメッセージ範囲の下限</param>
    /// <param name="wMsgFilterMax">取得するメッセージ範囲の上限</param>
    /// <returns>WM_QUIT なら 0、エラーなら -1、それ以外は 0 以外</returns>
    [DllImport("user32.dll")]
    internal static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    /// <summary>仮想キーメッセージを文字メッセージへ変換する</summary>
    /// <param name="lpMsg">変換するメッセージ</param>
    /// <returns>変換されたかどうか</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref MSG lpMsg);

    /// <summary>メッセージをウィンドウプロシージャへ送る</summary>
    /// <param name="lpMsg">送るメッセージ</param>
    /// <returns>ウィンドウプロシージャの戻り値</returns>
    [DllImport("user32.dll")]
    internal static extern nint DispatchMessage(ref MSG lpMsg);

    /// <summary>指定スレッドのメッセージキューへメッセージを投げる</summary>
    /// <param name="idThread">対象スレッド ID</param>
    /// <param name="msg">メッセージ種別</param>
    /// <param name="wParam">メッセージ種別に依存する値</param>
    /// <param name="lParam">メッセージ種別に依存する値</param>
    /// <returns>成功したかどうか</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessage(uint idThread, uint msg, nint wParam, nint lParam);

    /// <summary>現在のスレッドの ID を取得する</summary>
    /// <returns>現在のスレッド ID</returns>
    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();
}
