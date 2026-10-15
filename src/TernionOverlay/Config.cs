using System.Text.Json;
using TernionCore;

namespace TernionOverlay;

public enum AvatarSide { Left, Right, None }
public enum NameMode { Both, Nick }

/// <summary>Настройки оверлея (json в %AppData%\TernionOverlay\config.json).</summary>
public sealed class AppConfig
{
    public int Port { get; set; } = 5899;
    public bool ShowAll { get; set; } = true;          // true = всех, false = только говорящих
    public string NameMode { get; set; } = "both";      // both | nick
    public string Alignment { get; set; } = "left";      // left | right
    public string AvatarSide { get; set; } = "right";   // left | right | none
    public bool Background { get; set; } = true;
    public int BackgroundOpacity { get; set; } = 60;    // 0..100
    public bool ClickThrough { get; set; } = true;
    public bool AutoStartWindows { get; set; } = false;
    public bool AutoStartWithTs { get; set; } = true;
    public string Hotkey { get; set; } = "Alt+T";
    public int WindowX { get; set; } = 200;
    public int WindowY { get; set; } = 120;
    public int FontSize { get; set; } = 14;
    public int WindowWidth { get; set; } = 300;
    public string Language { get; set; } = "en";
    public int? UiRevision { get; set; } = 3;
    public bool Locked { get; set; } = true;

    public string AppDir { get; set; } = ""; // путь к приложению (для автозапуска)

    public NameMode NameModeEnum => (NameMode)Enum.Parse(typeof(NameMode), NameMode, true);
    public AvatarSide AvatarEnum => (AvatarSide)Enum.Parse(typeof(AvatarSide), AvatarSide, true);
}

public static class ConfigStore
{
    public static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TernionOverlay");

    private static string ConfigFile => Path.Combine(BaseDir, "config.json");
    private static string KeyFile => Path.Combine(BaseDir, "apikey.txt");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                // Older releases persisted Russian in AppData, even when the exe was deleted.
                // Migrate the presentation language once; preserve all functional settings.
                var json = File.ReadAllText(ConfigFile);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty(nameof(AppConfig.UiRevision), out _))
                {
                    cfg.Language = "en";
                    cfg.UiRevision = 2;
                }
                // The earlier nickname-only setting hides TS6 descriptions even when
                // Remote Apps has supplied them. Enable names on this one-time upgrade;
                // users can still select nickname-only again afterwards.
                if (cfg.UiRevision.GetValueOrDefault() < 3)
                {
                    cfg.NameMode = "both";
                    cfg.UiRevision = 3;
                    Save(cfg);
                }
                return cfg;
            }
        }
        catch { }
        return new AppConfig();
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static IKeyStore CreateKeyStore() => new DiskKeyStore(KeyFile);

    private sealed class DiskKeyStore(string file) : IKeyStore
    {
        public string? Load() { try { return File.Exists(file) ? File.ReadAllText(file).Trim() : null; } catch { return null; } }
        public void Save(string key) { try { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, key); } catch { } }
    }
}