using System.Text.Json.Serialization;

namespace AutomationToolkit.Core.Models;

/// <summary>キーボード操作ステップの共通基底</summary>
public abstract class KeyStepBase : MacroStep {
	/// <summary>Win32 仮想キーコード</summary>
	public ushort virtualKey { get; set; }
	/// <summary>ハードウェアスキャンコード</summary>
	/// <remarks>ゲーム互換性のため再生時はこちらを優先し、0 なら virtualKey にフォールバックする</remarks>
	public ushort scanCode { get; set; }
	/// <summary>矢印キーや右 Ctrl などの拡張キーかどうか</summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool isExtended { get; set; }
}

/// <summary>キーを押すステップ</summary>
public sealed class KeyDownStep : KeyStepBase;

/// <summary>キーを離すステップ</summary>
public sealed class KeyUpStep : KeyStepBase;
