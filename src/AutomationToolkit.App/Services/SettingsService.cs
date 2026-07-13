using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationToolkit.App.Services;

/// <summary>%APPDATA%\AutomationToolkit\settings.json への設定の読み書きを担当する</summary>
public sealed class SettingsService {
	/// <summary>設定ファイル用の JsonSerializerOptions</summary>
	private static readonly JsonSerializerOptions jsonOptions = new() {
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() },
	};

	/// <summary>設定ファイルを配置するフォルダのパス</summary>
	public string settingsDirectory { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutomationToolkit");

	/// <summary>設定ファイルのパス</summary>
	private string settingsPath => Path.Combine(settingsDirectory, "settings.json");
	/// <summary>マクロフォルダ未設定時に使う既定のフォルダのパス</summary>
	public string defaultMacrosFolder => Path.Combine(settingsDirectory, "macros");

	/// <summary>設定を読み込む</summary>
	/// <remarks>ファイルがない場合や壊れている場合は既定値にフォールバックする</remarks>
	/// <returns>読み込んだ設定</returns>
	public AppSettings Load() {
		try {
			if (File.Exists(settingsPath)) {
				var json = File.ReadAllText(settingsPath);
				var settings = JsonSerializer.Deserialize<AppSettings>(json, jsonOptions);
				if (settings is not null) {
					settings.macrosFolder ??= defaultMacrosFolder;
					return settings;
				}
			}
		}
		catch (Exception ex) when (ex is JsonException or IOException) {
			// 壊れた設定でも起動できるよう既定値にフォールバック
		}
		return new AppSettings { macrosFolder = defaultMacrosFolder };
	}

	/// <summary>設定を保存する</summary>
	/// <param name="settings">保存する設定</param>
	public void Save(AppSettings settings) {
		Directory.CreateDirectory(settingsDirectory);
		var json = JsonSerializer.Serialize(settings, jsonOptions);
		File.WriteAllText(settingsPath, json);
	}
}
