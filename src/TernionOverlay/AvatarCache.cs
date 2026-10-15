using System.Security.Cryptography;
using System.Text;

namespace TernionOverlay;

/// <summary>Скачивание и кэширование аватаров myTeamSpeak (фоном, без блокировки UI).</summary>
public sealed class AvatarCache
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Image?> _img = new();
    private readonly HashSet<string> _loading = new();
    private readonly Action _changed;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly string _dir;

    public AvatarCache(Action changed)
    {
        _changed = changed;
        try { _dir = Path.Combine(ConfigStore.BaseDir, "avatars"); Directory.CreateDirectory(_dir); }
        catch { _dir = ""; }
    }

    public Image? Get(string url)
    {
        lock (_lock) { if (_img.TryGetValue(url, out var im)) return im; }
        _ = LoadAsync(url);
        return null;
    }

    private async Task LoadAsync(string url)
    {
        lock (_lock) { if (_loading.Contains(url)) return; _loading.Add(url); }
        try
        {
            string key;
            try
            {
                key = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(url)))
                    .TrimEnd('=').Replace('/', '_').Replace('+', '-');
            }
            catch { key = url.GetHashCode().ToString(); }

            string path = Path.Combine(_dir, key + ".png");
            if (!File.Exists(path))
            {
                try
                {
                    var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
                    // проверим, что это реально картинка
                    using (var probe = Image.FromStream(new MemoryStream(bytes))) { }
                    File.WriteAllBytes(path, bytes);
                }
                catch { /* не получилось — оставим без аватара */ }
            }

            Image? img = null;
            try { if (File.Exists(path)) img = Image.FromFile(path); } catch { }
            lock (_lock) { _img[url] = img; _loading.Remove(url); }
            _changed();
        }
        catch
        {
            lock (_lock) { _loading.Remove(url); }
        }
    }
}