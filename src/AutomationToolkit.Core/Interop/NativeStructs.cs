using System.Runtime.InteropServices;

namespace AutomationToolkit.Core.Interop;

/// <summary>SendInput に渡す入力イベント</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    /// <summary>入力種別を示す INPUT_* 値</summary>
    public uint type;
    /// <summary>入力種別ごとのデータ</summary>
    public InputUnion U;

    /// <summary>マーシャリング時の構造体サイズ</summary>
    public static readonly int Size = Marshal.SizeOf<INPUT>();
}

/// <summary>INPUT の入力種別ごとのデータを重ねた共用体</summary>
[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    /// <summary>マウス入力のデータ</summary>
    [FieldOffset(0)] public MOUSEINPUT mi;
    /// <summary>キーボード入力のデータ</summary>
    [FieldOffset(0)] public KEYBDINPUT ki;
}

/// <summary>合成するマウス入力のデータ</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    /// <summary>X 方向の移動量または絶対座標</summary>
    public int dx;
    /// <summary>Y 方向の移動量または絶対座標</summary>
    public int dy;
    /// <summary>ホイール回転量や X ボタン番号などの付加データ</summary>
    public uint mouseData;
    /// <summary>動作を示す MOUSEEVENTF_* フラグ</summary>
    public uint dwFlags;
    /// <summary>イベントのタイムスタンプ。0 でシステムが設定</summary>
    public uint time;
    /// <summary>イベントに関連付ける追加情報</summary>
    public nint dwExtraInfo;
}

/// <summary>合成するキーボード入力のデータ</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    /// <summary>仮想キーコード</summary>
    public ushort wVk;
    /// <summary>ハードウェアスキャンコード</summary>
    public ushort wScan;
    /// <summary>動作を示す KEYEVENTF_* フラグ</summary>
    public uint dwFlags;
    /// <summary>イベントのタイムスタンプ。0 でシステムが設定</summary>
    public uint time;
    /// <summary>イベントに関連付ける追加情報</summary>
    public nint dwExtraInfo;
}

/// <summary>スクリーン座標の点</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    /// <summary>X 座標</summary>
    public int x;
    /// <summary>Y 座標</summary>
    public int y;
}

/// <summary>低レベルキーボードフックが受け取るイベント情報</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    /// <summary>仮想キーコード</summary>
    public uint vkCode;
    /// <summary>ハードウェアスキャンコード</summary>
    public uint scanCode;
    /// <summary>拡張キーや注入入力を示す LLKHF_* フラグ</summary>
    public uint flags;
    /// <summary>イベントのタイムスタンプ</summary>
    public uint time;
    /// <summary>イベントに関連付けられた追加情報</summary>
    public nint dwExtraInfo;
}

/// <summary>低レベルマウスフックが受け取るイベント情報</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    /// <summary>カーソルのスクリーン座標</summary>
    public POINT pt;
    /// <summary>ホイール回転量や X ボタン番号などの付加データ</summary>
    public uint mouseData;
    /// <summary>注入入力を示す LLMHF_* フラグ</summary>
    public uint flags;
    /// <summary>イベントのタイムスタンプ</summary>
    public uint time;
    /// <summary>イベントに関連付けられた追加情報</summary>
    public nint dwExtraInfo;
}

/// <summary>ウィンドウメッセージ</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    /// <summary>宛先ウィンドウのハンドル</summary>
    public nint hwnd;
    /// <summary>メッセージ種別</summary>
    public uint message;
    /// <summary>メッセージ種別に依存する値</summary>
    public nint wParam;
    /// <summary>メッセージ種別に依存する値</summary>
    public nint lParam;
    /// <summary>メッセージが投函された時刻</summary>
    public uint time;
    /// <summary>メッセージが投函されたときのカーソル座標</summary>
    public POINT pt;
}

/// <summary>Win32 API の定数定義</summary>
internal static class Win32
{
    // INPUT.type
    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;

    // MOUSEEVENTF_*
    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_XDOWN = 0x0080;
    public const uint MOUSEEVENTF_XUP = 0x0100;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const uint MOUSEEVENTF_HWHEEL = 0x1000;
    public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    public const uint XBUTTON1 = 1;
    public const uint XBUTTON2 = 2;

    // KEYEVENTF_*
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    // GetSystemMetrics
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    // MapVirtualKey
    public const uint MAPVK_VK_TO_VSC = 0;

    // フック
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const uint LLKHF_EXTENDED = 0x01;
    public const uint LLKHF_INJECTED = 0x10;
    public const uint LLMHF_INJECTED = 0x01;

    // ウィンドウメッセージ
    public const uint WM_QUIT = 0x0012;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MOUSEWHEEL = 0x020A;
    public const uint WM_XBUTTONDOWN = 0x020B;
    public const uint WM_XBUTTONUP = 0x020C;
    public const uint WM_MOUSEHWHEEL = 0x020E;

    // 仮想キーコード
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;
}
