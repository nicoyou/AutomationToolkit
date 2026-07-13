using System.Text.Json;
using AutomationToolkit.Core.Models;
using AutomationToolkit.Core.Persistence;

namespace AutomationToolkit.Core.Tests;

/// <summary>マクロの JSON シリアライズ/デシリアライズのテスト</summary>
public class SerializationTests {
	/// <summary>全ステップ種別を含むサンプルマクロを生成する</summary>
	/// <returns>サンプルマクロ</returns>
	private static Macro CreateSampleMacro() => new() {
		name = "sample",
		createdUtc = new DateTimeOffset(2026, 7, 12, 9, 30, 0, TimeSpan.Zero),
		steps =
		[
			new MouseMoveStep { delayBeforeMs = 120, x = 960, y = 540 },
			new MouseDownStep { delayBeforeMs = 35, button = MouseButton.Left, x = 960, y = 540 },
			new MouseUpStep { delayBeforeMs = 90, button = MouseButton.Left, x = 960, y = 540 },
			new KeyDownStep { delayBeforeMs = 300, virtualKey = 65, scanCode = 30 },
			new KeyUpStep { delayBeforeMs = 60, virtualKey = 65, scanCode = 30 },
			new MouseWheelStep { delayBeforeMs = 200, x = 960, y = 540, delta = -120 },
		],
	};

	/// <summary>シリアライズと逆変換で全ステップの内容が保持される</summary>
	[Fact]
	public void RoundTrip_PreservesAllSteps() {
		var macro = CreateSampleMacro();

		var json = JsonSerializer.Serialize(macro, MacroJson.Default);
		var restored = JsonSerializer.Deserialize<Macro>(json, MacroJson.Default);

		Assert.NotNull(restored);
		Assert.Equal(macro.schemaVersion, restored.schemaVersion);
		Assert.Equal(macro.name, restored.name);
		Assert.Equal(macro.steps.Count, restored.steps.Count);

		var move = Assert.IsType<MouseMoveStep>(restored.steps[0]);
		Assert.Equal((120, 960, 540), (move.delayBeforeMs, move.x, move.y));

		var down = Assert.IsType<MouseDownStep>(restored.steps[1]);
		Assert.Equal(MouseButton.Left, down.button);

		var keyDown = Assert.IsType<KeyDownStep>(restored.steps[3]);
		Assert.Equal((65, 30, false), ((int)keyDown.virtualKey, (int)keyDown.scanCode, keyDown.isExtended));

		var wheel = Assert.IsType<MouseWheelStep>(restored.steps[5]);
		Assert.Equal(-120, wheel.delta);
		Assert.False(wheel.isHorizontal);
	}

	/// <summary>期待した型判別子と camelCase のプロパティ名でシリアライズされる</summary>
	[Fact]
	public void Serialize_UsesExpectedTypeDiscriminatorsAndCamelCase() {
		var json = JsonSerializer.Serialize(CreateSampleMacro(), MacroJson.Default);

		Assert.Contains("\"$type\": \"mouseMove\"", json);
		Assert.Contains("\"$type\": \"mouseDown\"", json);
		Assert.Contains("\"$type\": \"keyDown\"", json);
		Assert.Contains("\"$type\": \"mouseWheel\"", json);
		Assert.Contains("\"button\": \"Left\"", json);
		Assert.Contains("\"delayBeforeMs\": 120", json);
		Assert.Contains("\"schemaVersion\": 1", json);
	}

	/// <summary>既定値のプロパティは JSON に出力されない</summary>
	[Fact]
	public void Serialize_OmitsDefaultValues() {
		var macro = new Macro {
			name = "minimal",
			steps = [new MouseMoveStep { x = 10, y = 20 }], // delayBeforeMs は既定値のまま
		};

		var json = JsonSerializer.Serialize(macro, MacroJson.Default);

		Assert.DoesNotContain("delayBeforeMs", json);
		Assert.DoesNotContain("isExtended", json);
	}

	/// <summary>手書き・手編集を想定した最小 JSON をデシリアライズできる</summary>
	[Fact]
	public void Deserialize_HandEditedJson_Works() {
		// ユーザーが手書き・手編集する想定の最小 JSON
		const string json = """
		{
		  "schemaVersion": 1,
		  "name": "hand-written",
		  "steps": [
		    { "$type": "mouseMove", "delayBeforeMs": 120, "x": 960, "y": 540 },
		    { "$type": "mouseDown", "button": "Right", "x": 960, "y": 540 },
		    { "$type": "keyDown", "virtualKey": 65, "scanCode": 30 }
		  ]
		}
		""";

		var macro = JsonSerializer.Deserialize<Macro>(json, MacroJson.Default);

		Assert.NotNull(macro);
		Assert.Equal(3, macro.steps.Count);
		Assert.Equal(MouseButton.Right, Assert.IsType<MouseDownStep>(macro.steps[1]).button);
		Assert.Equal(0, macro.steps[1].delayBeforeMs);
	}
}
