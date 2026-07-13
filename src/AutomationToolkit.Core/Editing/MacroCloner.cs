using System.Text.Json;
using AutomationToolkit.Core.Models;
using AutomationToolkit.Core.Persistence;

namespace AutomationToolkit.Core.Editing;

/// <summary>マクロステップ列の deep clone を提供する</summary>
public static class MacroCloner {
	/// <summary>ステップ列を JSON ラウンドトリップで複製する</summary>
	/// <remarks>$type 判別子付きでシリアライズされるため派生型が保たれる</remarks>
	/// <param name="steps">複製するステップ列</param>
	/// <returns>複製されたステップ列</returns>
	public static List<MacroStep> CloneSteps(IReadOnlyList<MacroStep> steps) {
		var json = JsonSerializer.SerializeToUtf8Bytes(steps, MacroJson.Default);
		return JsonSerializer.Deserialize<List<MacroStep>>(json, MacroJson.Default)
			?? throw new InvalidOperationException("ステップ列の複製に失敗しました");
	}
}
