using AutomationToolkit.Core.Models;
using AutomationToolkit.Core.Persistence;

namespace AutomationToolkit.Core.Tests;

/// <summary>MacroRepository のファイル入出力のテスト</summary>
public class MacroRepositoryTests : IDisposable {
	/// <summary>テストごとに使い捨てる一時フォルダのパス</summary>
	private readonly string tempDir =
		Path.Combine(Path.GetTempPath(), "atk-tests-" + Guid.NewGuid().ToString("N"));

	/// <summary>テストで作成した一時フォルダを削除する</summary>
	public void Dispose() {
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
	}

	/// <summary>保存したマクロを読み込むと内容が保持されている</summary>
	[Fact]
	public void SaveAndLoad_RoundTrips() {
		var repo = new MacroRepository(tempDir);
		var macro = new Macro {
			name = "test-macro",
			steps = [new KeyDownStep { virtualKey = 65, scanCode = 30, delayBeforeMs = 50 }],
		};

		var path = repo.Save(macro);
		var loaded = repo.Load(path);

		Assert.Equal("test-macro", loaded.name);
		var step = Assert.IsType<KeyDownStep>(Assert.Single(loaded.steps));
		Assert.Equal(50, step.delayBeforeMs);
		Assert.Equal(path, Assert.Single(repo.ListMacroFiles()));
	}

	/// <summary>壊れた JSON を読み込むと MacroFormatException が発生する</summary>
	[Fact]
	public void Load_CorruptJson_ThrowsMacroFormatException() {
		Directory.CreateDirectory(tempDir);
		var path = Path.Combine(tempDir, "broken.json");
		File.WriteAllText(path, "{ this is not json");

		Assert.Throws<MacroFormatException>(() => new MacroRepository(tempDir).Load(path));
	}

	/// <summary>対応バージョンより新しいスキーマバージョンを読み込むと MacroFormatException が発生する</summary>
	[Fact]
	public void Load_NewerSchemaVersion_ThrowsMacroFormatException() {
		Directory.CreateDirectory(tempDir);
		var path = Path.Combine(tempDir, "future.json");
		File.WriteAllText(path, """{ "schemaVersion": 999, "name": "future", "steps": [] }""");

		var ex = Assert.Throws<MacroFormatException>(() => new MacroRepository(tempDir).Load(path));
		Assert.Contains("999", ex.Message);
	}

	/// <summary>ファイル名に使えない文字が保存先パスから除去される</summary>
	[Fact]
	public void GetPathFor_SanitizesInvalidFileNameChars() {
		var repo = new MacroRepository(tempDir);

		var path = repo.GetPathFor("bad/name:with*chars?");

		Assert.EndsWith(".json", path);
		Assert.Equal(-1, Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()));
	}

	/// <summary>フォルダが存在しない場合は空の一覧が返る</summary>
	[Fact]
	public void ListMacroFiles_MissingFolder_ReturnsEmpty() {
		var repo = new MacroRepository(Path.Combine(tempDir, "does-not-exist"));

		Assert.Empty(repo.ListMacroFiles());
	}
}
