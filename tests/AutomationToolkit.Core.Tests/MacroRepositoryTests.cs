using System.Text;
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

	/// <summary>指定したパスへ上書き保存したマクロを読み込むと内容が保持されている</summary>
	[Fact]
	public void SaveTo_OverwritesSpecifiedFile() {
		var repo = new MacroRepository(tempDir);
		var path = repo.Save(new Macro { name = "original" });

		var edited = repo.Load(path);
		edited.steps.Add(new MouseDownStep { button = MouseButton.Left, x = 1, y = 2, delayBeforeMs = 50 });
		repo.SaveTo(path, edited);

		var loaded = repo.Load(path);
		var step = Assert.IsType<MouseDownStep>(Assert.Single(loaded.steps));
		Assert.Equal(50, step.delayBeforeMs);
		Assert.Equal(path, Assert.Single(repo.ListMacroFiles()));
	}

	/// <summary>マクロ名から導出されるパスと異なるパスへも保存できる</summary>
	[Fact]
	public void SaveTo_WritesToPathDifferentFromNameDerivedPath() {
		var repo = new MacroRepository(tempDir);
		Directory.CreateDirectory(tempDir);
		var path = Path.Combine(tempDir, "renamed-file.json");

		repo.SaveTo(path, new Macro { name = "違う名前のマクロ" });

		Assert.Equal("違う名前のマクロ", repo.Load(path).name);
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

	/// <summary>名前を変更するとファイル名はそのままで中身の名前だけ書き換わる</summary>
	[Fact]
	public void Rename_UpdatesNameWithoutMovingFile() {
		var repo = new MacroRepository(tempDir);
		var path = repo.Save(new Macro { name = "before" });

		repo.Rename(path, "after");

		Assert.Equal("after", repo.Load(path).name);
		Assert.Equal(path, Assert.Single(repo.ListMacroFiles()));
	}

	/// <summary>壊れたマクロの名前を変更しようとすると MacroFormatException が発生する</summary>
	[Fact]
	public void Rename_CorruptJson_ThrowsMacroFormatException() {
		Directory.CreateDirectory(tempDir);
		var path = Path.Combine(tempDir, "broken.json");
		File.WriteAllText(path, "{ this is not json");

		Assert.Throws<MacroFormatException>(() => new MacroRepository(tempDir).Rename(path, "after"));
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

	/// <summary>行番号付き読み込みで全ステップ分の行番号が得られ、各行がステップの開始行を指す</summary>
	[Fact]
	public void LoadWithStepLines_ReturnsLineForEachStep() {
		var repo = new MacroRepository(tempDir);
		var macro = new Macro {
			name = "with-lines",
			steps =
			[
				new MouseMoveStep { x = 1, y = 2, delayBeforeMs = 10 },
				new KeyDownStep { virtualKey = 65, scanCode = 30 },
			],
		};
		var path = repo.Save(macro);

		var loaded = repo.LoadWithStepLines(path);

		Assert.Equal(loaded.macro.steps.Count, loaded.stepLines.Count);
		// WriteIndented では各ステップは "{" のみの行から始まる
		var lines = File.ReadAllLines(path);
		Assert.All(loaded.stepLines, line => Assert.Equal("{", lines[line - 1].Trim()));
	}

	/// <summary>BOM 付きのマクロファイルも行番号付きで読み込める</summary>
	[Fact]
	public void LoadWithStepLines_Utf8Bom_Works() {
		Directory.CreateDirectory(tempDir);
		var path = Path.Combine(tempDir, "bom.json");
		const string json = """
		{
		  "schemaVersion": 1,
		  "name": "bom",
		  "steps": [
		    { "$type": "mouseMove", "x": 1, "y": 2 }
		  ]
		}
		""";
		File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

		var loaded = new MacroRepository(tempDir).LoadWithStepLines(path);

		Assert.Equal("bom", loaded.macro.name);
		Assert.Equal([5], loaded.stepLines);
	}
}
