using System.Text.Json;
using AutomationToolkit.Core.Models;

namespace AutomationToolkit.Core.Persistence;

/// <summary>マクロファイルの内容が不正・非対応のときに投げる例外</summary>
/// <param name="message">エラーメッセージ</param>
/// <param name="inner">原因となった例外</param>
public sealed class MacroFormatException(string message, Exception? inner = null)
	: Exception(message, inner);

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
	public Macro Load(string filePath) {
		Macro? macro;
		try {
			using var stream = File.OpenRead(filePath);
			macro = JsonSerializer.Deserialize<Macro>(stream, MacroJson.Default);
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
		return macro;
	}

	/// <summary>マクロ名から決めた保存先パスへマクロを保存する</summary>
	/// <param name="macro">保存するマクロ</param>
	/// <returns>保存先のファイルパス</returns>
	public string Save(Macro macro) {
		Directory.CreateDirectory(macrosFolder);
		var path = GetPathFor(macro.name);
		using var stream = File.Create(path);
		JsonSerializer.Serialize(stream, macro, MacroJson.Default);
		return path;
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
