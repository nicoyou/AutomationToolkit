using System.Text.Json.Serialization;

namespace AutomationToolkit.Core.Models;

/// <summary>マウス操作ステップの共通基底</summary>
/// <remarks>座標は仮想スクリーン空間の物理ピクセルで、マルチモニタでは負値もあり得る</remarks>
public abstract class MouseStepBase : MacroStep {
	/// <summary>カーソルの X 座標</summary>
	public int x { get; set; }
	/// <summary>カーソルの Y 座標</summary>
	public int y { get; set; }
}

/// <summary>カーソルを移動するステップ</summary>
public sealed class MouseMoveStep : MouseStepBase;

/// <summary>マウスボタンを押すステップ</summary>
public sealed class MouseDownStep : MouseStepBase {
	/// <summary>対象のマウスボタン</summary>
	public MouseButton button { get; set; }
}

/// <summary>マウスボタンを離すステップ</summary>
public sealed class MouseUpStep : MouseStepBase {
	/// <summary>対象のマウスボタン</summary>
	public MouseButton button { get; set; }
}

/// <summary>マウスホイールを回転するステップ</summary>
public sealed class MouseWheelStep : MouseStepBase {
	/// <summary>1 ノッチを ±120 とするホイール回転量</summary>
	public int delta { get; set; }
	/// <summary>水平ホイールかどうか</summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool isHorizontal { get; set; }
}
