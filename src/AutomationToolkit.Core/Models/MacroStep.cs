using System.Text.Json.Serialization;

namespace AutomationToolkit.Core.Models;

/// <summary>マクロを構成する 1 操作</summary>
/// <remarks>
/// 派生型は $type 判別子で JSON にシリアライズされる
/// 将来 ImageSearchStep などを追加する場合は JsonDerivedType 属性を 1 行足すだけでよい
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(KeyDownStep), "keyDown")]
[JsonDerivedType(typeof(KeyUpStep), "keyUp")]
[JsonDerivedType(typeof(MouseDownStep), "mouseDown")]
[JsonDerivedType(typeof(MouseUpStep), "mouseUp")]
[JsonDerivedType(typeof(MouseMoveStep), "mouseMove")]
[JsonDerivedType(typeof(MouseWheelStep), "mouseWheel")]
public abstract class MacroStep {
	/// <summary>このステップを実行する前に待機するミリ秒数</summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public int delayBeforeMs { get; set; }
}
