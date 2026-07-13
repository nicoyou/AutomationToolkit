using System.Text;

namespace AutomationToolkit.Core.Models;

/// <summary>ホットキーの修飾キーの組み合わせ</summary>
[Flags]
public enum ChordModifiers {
	/// <summary>修飾キーなし</summary>
	None = 0,
	/// <summary>Ctrl キー</summary>
	Control = 1,
	/// <summary>Alt キー</summary>
	Alt = 2,
	/// <summary>Shift キー</summary>
	Shift = 4,
	/// <summary>Windows キー</summary>
	Win = 8,
}

/// <summary>修飾キーとメインキーで構成されるグローバルホットキーのキーコンボ</summary>
/// <param name="modifiers">修飾キーの組み合わせ</param>
/// <param name="virtualKey">メインキーの仮想キーコード</param>
public sealed record HotkeyChord(ChordModifiers modifiers, ushort virtualKey) {
	/// <summary>Ctrl+Alt+F1 のような表示用文字列を返す</summary>
	/// <returns>ホットキーの表示用文字列</returns>
	public override string ToString() {
		var builder = new StringBuilder();
		if (modifiers.HasFlag(ChordModifiers.Control)) builder.Append("Ctrl+");
		if (modifiers.HasFlag(ChordModifiers.Alt)) builder.Append("Alt+");
		if (modifiers.HasFlag(ChordModifiers.Shift)) builder.Append("Shift+");
		if (modifiers.HasFlag(ChordModifiers.Win)) builder.Append("Win+");
		builder.Append(VirtualKeyNames.GetName(virtualKey));
		return builder.ToString();
	}
}

/// <summary>仮想キーコードの表示名を提供する</summary>
public static class VirtualKeyNames {
	/// <summary>仮想キーコードに対応する表示名を返す</summary>
	/// <param name="virtualKey">仮想キーコード</param>
	/// <returns>キーの表示名</returns>
	public static string GetName(ushort virtualKey) => virtualKey switch {
		>= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),          // 0-9
		>= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),          // A-Z
		>= 0x70 and <= 0x87 => $"F{virtualKey - 0x70 + 1}",            // F1-F24
		>= 0x60 and <= 0x69 => $"Num{virtualKey - 0x60}",              // テンキー 0-9
		0x08 => "Backspace",
		0x09 => "Tab",
		0x0D => "Enter",
		0x13 => "Pause",
		0x1B => "Esc",
		0x20 => "Space",
		0x21 => "PageUp",
		0x22 => "PageDown",
		0x23 => "End",
		0x24 => "Home",
		0x25 => "Left",
		0x26 => "Up",
		0x27 => "Right",
		0x28 => "Down",
		0x2C => "PrintScreen",
		0x2D => "Insert",
		0x2E => "Delete",
		_ => $"VK_0x{virtualKey:X2}",
	};
}
