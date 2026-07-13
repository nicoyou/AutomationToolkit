using System.Text.Json;
using AutomationToolkit.Core.Interop;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Persistence;

/// <summary>マクロファイルの内容が不正・非対応のときに投げる例外</summary>
/// <param name="message">エラーメッセージ</param>
/// <param name="inner">原因となった例外</param>
public sealed class MacroFormatException(string message, Exception? inner = null)
	: Exception(message, inner);

/// <summary>読み込んだマクロと steps 各要素の JSON 行番号</summary>
/// <param name="macro">読み込んだマクロ</param>
/// <param name="stepLines">steps 配列の各要素が始まる 1 始まりの行番号の一覧</param>
public sealed record LoadedMacro(Macro macro, IReadOnlyList<int> stepLines);

/// <summary>macros フォルダ内の JSON マクロファイルの読み書きを担当する</summary>
/// <param name="folder">マクロファイルを保存するフォルダのパス</param>
public sealed class MacroRepository(string folder) {
	/// <summary>マクロファイルを保存するフォルダの絶対パス</summary>
	public string macrosFolder { get; } = Path.GetFullPath(folder);

	/// <summary>フォルダ内のマクロファイルを列挙する</summary>
	/// <returns>ファイル名順に並んだマクロファイルのパスの一覧</returns>
	public IReadOnlyList<string> ListMacroFiles() {
		if (Directory.Exists(macrosFolder) == false) return [];
		return Directory.EnumerateFiles(macrosFolder, "*.json")
			.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <summary>マクロファイルを読み込む</summary>
	/// <param name="filePath">読み込むマクロファイルのパス</param>
	/// <returns>読み込んだマクロ</returns>
	/// <exception cref="MacroFormatException">JSON が不正、またはスキーマバージョンが非対応の場合</exception>
	public Macro Load(string filePath) => LoadWithStepLines(filePath).macro;

	/// <summary>マクロファイルを steps の行番号情報付きで読み込む</summary>
	/// <remarks>外部エディタでの保存と競合しないよう、同一のバイト列からマクロと行番号の両方を求める</remarks>
	/// <param name="filePath">読み込むマクロファイルのパス</param>
	/// <returns>読み込んだマクロと steps 各要素の行番号</returns>
	/// <exception cref="MacroFormatException">JSON が不正、またはスキーマバージョンが非対応の場合</exception>
	public LoadedMacro LoadWithStepLines(string filePath) {
		var bytes = File.ReadAllBytes(filePath);
		// Utf8JsonReader は BOM を受け付けないため先頭から除去する ( BOM は改行を含まないので行番号はずれない )
		ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
		var utf8Json = bytes.AsSpan();
		if (utf8Json.StartsWith(utf8Bom)) utf8Json = utf8Json[utf8Bom.Length..];

		Macro? macro;
		try {
			macro = JsonSerializer.Deserialize<Macro>(utf8Json, MacroJson.Default);
		}
		catch (JsonException ex) {
			throw new MacroFormatException($"マクロファイルの JSON が不正です: {filePath}", ex);
		}

		if (macro is null) throw new MacroFormatException($"マクロファイルが空です: {filePath}");
		if (macro.schemaVersion > Macro.CURRENT_SCHEMA_VERSION) {
			throw new MacroFormatException(
				$"schemaVersion {macro.schemaVersion} は新しすぎます (対応バージョン: {Macro.CURRENT_SCHEMA_VERSION})。" +
				$"新しいバージョンのアプリで作成されたファイルです: {filePath}");
		}
		return new LoadedMacro(macro, MacroStepLineLocator.LocateStepLines(utf8Json));
	}

	/// <summary>マクロ名から決めた保存先パスへマクロを保存する</summary>
	/// <param name="macro">保存するマクロ</param>
	/// <returns>保存先のファイルパス</returns>
	public string Save(Macro macro) {
		Directory.CreateDirectory(macrosFolder);
		var path = GetPathFor(macro.name);
		SaveTo(path, macro);
		return path;
	}

	/// <summary>指定したファイルパスへマクロを上書き保存する</summary>
	/// <param name="filePath">保存先のマクロファイルのパス</param>
	/// <param name="macro">保存するマクロ</param>
	public void SaveTo(string filePath, Macro macro) {
		using var stream = File.Create(filePath);
		JsonSerializer.Serialize(stream, macro, MacroJson.Default);
	}

	/// <summary>マクロの名前を変更し、同じファイルへ上書き保存する</summary>
	/// <remarks>ファイル名は変更しない</remarks>
	/// <param name="filePath">名前を変更するマクロファイルのパス</param>
	/// <param name="newName">変更後のマクロ名</param>
	/// <exception cref="MacroFormatException">元ファイルが読み込めない場合</exception>
	public void Rename(string filePath, string newName) {
		var macro = Load(filePath);
		macro.name = newName;
		SaveTo(filePath, macro);
	}

	/// <summary>マクロファイルをゴミ箱へ移動する</summary>
	/// <param name="filePath">削除するマクロファイルのパス</param>
	/// <exception cref="FileNotFoundException">ファイルが存在しない場合</exception>
	/// <exception cref="IOException">ゴミ箱への移動に失敗した場合</exception>
	public void Delete(string filePath) {
		var fullPath = Path.GetFullPath(filePath);
		if (File.Exists(fullPath) == false) {
			throw new FileNotFoundException($"マクロファイルが見つかりません: {fullPath}", fullPath);
		}
		var operation = new SHFILEOPSTRUCT {
			wFunc = Win32.FO_DELETE,
			pFrom = fullPath + "\0", // マーシャリングで付く終端と合わせて二重の null 終端にする
			fFlags = (ushort)(Win32.FOF_ALLOWUNDO | Win32.FOF_NOCONFIRMATION | Win32.FOF_SILENT | Win32.FOF_NOERRORUI),
		};
		var result = NativeMethods.SHFileOperation(ref operation);
		if (result != 0 || operation.fAnyOperationsAborted) {
			throw new IOException($"ゴミ箱への移動に失敗しました ( エラーコード: {result} ): {fullPath}");
		}
	}

	/// <summary>マクロ名から保存先のファイルパスを求める</summary>
	/// <remarks>ファイル名に使えない文字は _ に置き換える</remarks>
	/// <param name="macroName">マクロ名</param>
	/// <returns>保存先のファイルパス</returns>
	public string GetPathFor(string macroName) {
		var fileName = string.Join("_", macroName.Split(Path.GetInvalidFileNameChars(),
			StringSplitOptions.RemoveEmptyEntries)).Trim();
		if (fileName.Length == 0) fileName = "unnamed";
		return Path.Combine(macrosFolder, fileName + ".json");
	}
}
