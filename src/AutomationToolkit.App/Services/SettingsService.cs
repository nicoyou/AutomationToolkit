using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationToolkit.App.Services;

/// <summary>%APPDATA%\AutomationToolkit\settings.json への設定の読み書きを担当する</summary>
public sealed class SettingsService
{
    /// <summary>設定ファイル用の JsonSerializerOptions</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>設定ファイルを配置するフォルダのパス</summary>
    public string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutomationToolkit");

    /// <summary>設定ファイルのパス</summary>
    private string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>マクロフォルダ未設定時に使う既定のフォルダのパス</summary>
    public string DefaultMacrosFolder => Path.Combine(SettingsDirectory, "macros");

    /// <summary>設定を読み込む</summary>
    /// <remarks>ファイルがない場合や壊れている場合は既定値にフォールバックする</remarks>
    /// <returns>読み込んだ設定</returns>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    settings.MacrosFolder ??= DefaultMacrosFolder;
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // 壊れた設定でも起動できるよう既定値にフォールバック
        }
        return new AppSettings { MacrosFolder = DefaultMacrosFolder };
    }

    /// <summary>設定を保存する</summary>
    /// <param name="settings">保存する設定</param>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
