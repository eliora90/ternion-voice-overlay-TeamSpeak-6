using System.Security.Cryptography;
using System.Text;

namespace TernionOverlay;

/// <summary>Скачивание и кэширование аватаров myTeamSpeak (фоном, без блокировки UI).</summary>
public sealed class AvatarCache
{
    /// <summary>Верхний предел размера аватара: чужой сервер не должен забить диск.</summary>
    private const long MaxAvatarBytes = 4L * 1024 * 1024;

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
        // Адрес аватара приходит из ответа клиента TeamSpeak: берём только
        // обычные веб-схемы, чтобы запрос не ушёл в локальный файл или иной обработчик.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return;

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
                    using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    var declared = resp.Content.Headers.ContentLength;
                    if (resp.IsSuccessStatusCode && (declared is null || declared <= MaxAvatarBytes))
                    {
                        await using var net = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        using var buffer = new MemoryStream();
                        var chunk = new byte[81920];
                        int read;
                        bool tooBig = false;
                        while ((read = await net.ReadAsync(chunk).ConfigureAwait(false)) > 0)
                        {
                            buffer.Write(chunk, 0, read);
                            if (buffer.Length > MaxAvatarBytes) { tooBig = true; break; }
                        }
                        if (!tooBig && buffer.Length > 0)
                        {
                            var bytes = buffer.ToArray();
                            // проверим, что это реально картинка
                            using (var probe = Image.FromStream(new MemoryStream(bytes))) { }
                            File.WriteAllBytes(path, bytes);
                        }
                    }
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