using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using TernionCore;

// Тестовый прогон ядра TernionCore на Linux: поднимаем двойник сервера TeamSpeak 6
// (как mock_ts6_server.py) и проверяем разбор auth, участников канала, события речи, перемещения.

public static class Program
{
    private static int _failures;

    public static async Task<int> Main()
    {
        const int port = 25899;
        using var mock = new MockTsServer(port);
        var started = mock.Start();
        if (!started) { Console.WriteLine("FAIL: mock не поднялся"); return 1; }

        var store = new MemoryKeyStore();
        var client = new Ts6Client(port, store, "com.ternion.test");
        client.ConnectionFailed += (m) => Console.WriteLine($"[diag] Error: {m}");
        client.ConnectedChanged += () => Console.WriteLine($"[diag] Connected={client.State.Connected}");
        var stateChanged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int ticks = 0;
        client.StateUpdated += () => { ticks++; if (client.State.ServerName.Length > 0 && client.State.OurChannelId != 0) stateChanged.TrySetResult(true); };
        client.Start();

        var got = await Task.WhenAny(stateChanged.Task, Task.Delay(8000)).ConfigureAwait(false);
        if (got != stateChanged.Task)
        {
            Console.WriteLine($"FAIL: не дождались auth (Connected={client.State.Connected} ticks={ticks} Err='{client.State.Error}')");
            return 1;
        }

        Check("сервер", client.State.ServerName, "TERNION");
        Check("наш канал", client.State.OurChannelName, "Канал #1");

        var visible = client.State.Visible.Select(p => p.Id).ToHashSet();
        Check("в канале есть eliora90", visible.Contains(35), true, "id 35 виден");
        Check("в канале есть nekroman23", visible.Contains(20), true);
        Check("НЕ в канале RKNcompany(37)", visible.Contains(37), false);

        var me = client.State.All[35];
        Check("описание eliora90 = Наташа", me.Description, "Наташа");
        Check("имя в подписи eliora90", me.DisplayName, "eliora90 (Наташа)");
        Check("аватар распарсен", me.AvatarUrl, "https://ex/av.png");
        Check("myTS тег", me.MyTsTag, "eliora90@myteamspeak.com");

        await Task.Delay(1800).ConfigureAwait(false);
        Check("обновление описания без переподключения", me.Description, "Наталья");
        // после clientMoved(id37 -> ch25) должно стать видимо
        Check("RKNcompany стал виден после clientMoved", client.State.Visible.Any(p => p.Id == 37), true);
        var rkn = client.State.All[37];
        Check("RKNcompany говорит (flag/статус)", rkn.Talking, true);

        Check("сохранён apiKey", store.Saved, "f6b1ff57-c902-4fd7-a1f5-d3356a96c248");

        // An authorized TS client can be open without joining any voice server.
        // Re-auth should later recover the channel list without another permission prompt.
        using var emptyThenJoined = new MockTsServer(25900, emptyFirst: true);
        if (!emptyThenJoined.Start()) { Console.WriteLine("FAIL: second mock не поднялся"); return 1; }
        var second = new Ts6Client(25900, new MemoryKeyStore(), "com.ternion.testempty");
        var noServer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var joined = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        second.StateUpdated += () => {
            if (second.State.Authorized && !second.State.HasServerConnection) noServer.TrySetResult(true);
            if (second.State.Authorized && second.State.HasServerConnection && second.State.OurChannelId == 25)
                joined.TrySetResult(true);
        };
        second.Start();
        await Task.WhenAny(noServer.Task, Task.Delay(5000));
        Check("авторизован, но нет сервера", noServer.Task.IsCompleted, true);
        await Task.WhenAny(joined.Task, Task.Delay(22000));
        Check("канал получен после входа на сервер", joined.Task.IsCompleted, true);
        await second.DisposeAsync();

        Console.WriteLine(_failures == 0 ? "=== ВСЕ ПРОВЕРКИ ПРОШЛИ ===" : $"=== ПРОВАЛОВ: {_failures} ===");
        return _failures == 0 ? 0 : 1;
    }

    private static void Check(string name, string got, string want) =>
        Report(name, string.Equals(got, want), $"ожидалось «{want}», получено «{got}»");
    private static void Check(string name, bool got, bool want) =>
        Report(name, got == want, $"получено {got}, ожидалось {want}");
    private static void Check(string name, bool got, bool want, string what) =>
        Report(name, got == want, $"{what}: {got}");

    private static void Report(string name, bool ok, string detail)
    {
        if (ok) Console.WriteLine($"  PASS  {name}");
        else { Console.WriteLine($"  FAIL  {name} — {detail}"); _failures++; }
    }
}

internal sealed class MemoryKeyStore : IKeyStore
{
    public string? Saved { get; private set; }
    public string? Load() => null;
    public void Save(string key) => Saved = key;
}

/// <summary>Минимальный WebSocket-сервер-двойник TeamSpeak 6 (типа mock_ts6_server.py).</summary>
internal sealed class MockTsServer : IDisposable
{
    private readonly int _port;
    private readonly bool _emptyFirst;
    private int _accepted;
    private readonly TcpListener _listener;
    private CancellationTokenSource _cts = new();

    public MockTsServer(int port, bool emptyFirst = false) { _port = port; _emptyFirst = emptyFirst; _listener = new TcpListener(IPAddress.Any, _port); }

    public bool Start()
    {
        try { _listener.Start(); Task.Run(RunAsync); return true; }
        catch { return false; }
    }

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var tcp = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = HandleAsync(tcp, Interlocked.Increment(ref _accepted));
            }
            catch { break; }
        }
    }

    private async Task HandleAsync(TcpClient tcp, int index)
    {
        try
        {
            using (tcp)
            using (var stream = tcp.GetStream())
            {
                // HTTP-рукопожатие
                var header = await ReadUntil(stream, new byte[] { 13, 10, 13, 10 }, 8192).ConfigureAwait(false);
                var keyLine = Encoding.ASCII.GetString(header).Split('\n').First(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase));
                var key = keyLine[(keyLine.IndexOf(':') + 1)..].Trim();
                var accept = Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(
                    Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                var resp = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                           $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
                var respBytes = Encoding.ASCII.GetBytes(resp);
                await stream.WriteAsync(respBytes).ConfigureAwait(false);

                // ждём кадр auth от клиента
                await ReadMaskedTextFrame(stream).ConfigureAwait(false);

                await SendTextAsync(stream, _emptyFirst && index == 1
                    ? "{\"type\":\"auth\",\"status\":{\"code\":0},\"payload\":{\"connections\":[]}}"
                    : BuildAuth()).ConfigureAwait(false);
                await Task.Delay(400).ConfigureAwait(false);

                // status: 1 -> говорить
                await SendTextAsync(stream, Talk(20, 1)).ConfigureAwait(false);
                await Task.Delay(300).ConfigureAwait(false);
                await SendTextAsync(stream, Talk(20, 2)).ConfigureAwait(false); // 2 -> перестал
                await Task.Delay(300).ConfigureAwait(false);

                // переместить RKNcompany (37) из канала 26 в канал 25 (наш)
                await SendTextAsync(stream, "{\"payload\":{\"clientId\":37,\"newChannelId\":\"25\",\"oldChannelId\":\"26\"},\"type\":\"clientMoved\"}").ConfigureAwait(false);
                await SendTextAsync(stream, "{\"payload\":{\"clientId\":35,\"properties\":{\"description\":\"Наталья\"}},\"type\":\"clientPropertiesUpdated\"}").ConfigureAwait(false);
                await Task.Delay(1500).ConfigureAwait(false);
            }
        }
        catch { }
    }

    private static async Task<byte[]> ReadUntil(NetworkStream s, byte[] term, int max)
    {
        var buf = new List<byte>();
        while (buf.Count < max)
        {
            var b = new byte[1];
            var n = await s.ReadAsync(b).ConfigureAwait(false);
            if (n == 0) break;
            buf.Add(b[0]);
            if (buf.Count >= term.Length)
            {
                bool done = true;
                for (int k = 0; k < term.Length; k++)
                    if (buf[buf.Count - term.Length + k] != term[k]) { done = false; break; }
                if (done) break;
            }
        }
        return buf.ToArray();
    }

    private static async Task<(byte Opcode, byte[] Data)> ReadMaskedTextFrame(NetworkStream s)
    {
        var h = await ReadExact(s, 2).ConfigureAwait(false);
        int opcode = h[0] & 0x0F;
        bool masked = (h[1] & 0x80) != 0;
        long len = h[1] & 0x7F;
        if (len == 126) len = BitConverter.ToUInt16((await ReadExact(s, 2).ConfigureAwait(false)).Reverse().ToArray());
        else if (len == 127) len = (long)BitConverter.ToUInt64((await ReadExact(s, 8).ConfigureAwait(false)).Reverse().ToArray());
        byte[] mask = masked ? await ReadExact(s, 4).ConfigureAwait(false) : new byte[0];
        var data = await ReadExact(s, (int)len).ConfigureAwait(false);
        if (masked) for (int i = 0; i < data.Length; i++) data[i] ^= mask[i % 4];
        return ((byte)opcode, data);
    }

    private static async Task<byte[]> ReadExact(NetworkStream s, int count)
    {
        var buf = new byte[count];
        int off = 0;
        while (off < count)
        {
            int n = await s.ReadAsync(buf, off, count - off).ConfigureAwait(false);
            if (n == 0) break;
            off += n;
        }
        return buf[..off];
    }

    private static async Task SendTextAsync(NetworkStream s, string json)
    {
        var data = Encoding.UTF8.GetBytes(json);
        var header = new List<byte> { 0x81 };
        if (data.Length < 126) header.Add((byte)data.Length);
        else if (data.Length < 65536) { header.Add((byte)126); header.AddRange(BitConverter.GetBytes((ushort)data.Length).Reverse()); }
        else { header.Add((byte)127); header.AddRange(BitConverter.GetBytes((ulong)data.Length).Reverse()); }
        var payload = header.Concat(data).ToArray();
        await s.WriteAsync(payload).ConfigureAwait(false);
    }

    private static string Talk(long id, int status) =>
        $"{{\"payload\":{{\"clientId\":{id},\"connectionId\":3,\"isWhisper\":false,\"status\":{status}}},\"type\":\"talkStatusChanged\"}}";

    // Сборка auth-ответа как строго валидного JSON через JsonSerializer (не руками).
    private static string BuildAuth()
    {
        var root = new { id = "24", parentId = "0", properties = new { name = "ИГРОВЫЕ КАНАЛЫ:", uniqueIdentifier = "x1" } };
        var ch25 = new { id = "25", parentId = "24", properties = new { name = "Канал #1" } };
        var ch26 = new { id = "26", parentId = "24", properties = new { name = "Канал #2" } };

        var clients = new object[] {
            new { id = 35L, channelId = "25", properties = new { nickname = "eliora90", description = "Наташа", country = "RU", inputMuted = false, outputMuted = false, flagTalking = false, myteamspeakAvatar = "2,https://ex/av.png", myteamspeakId = "AQs/..", uniqueIdentifier = "HB/==", userTag = "{\"tag\":\"eliora90@myteamspeak.com\"}" } },
            new { id = 20L, channelId = "25", properties = new { nickname = "nekroman23", description = "", country = "RU", inputMuted = false, outputMuted = false, flagTalking = false, userTag = "{\"tag\":\"nekroman23@myteamspeak.com\"}" } },
            new { id = 21L, channelId = "25", properties = new { nickname = "AADRIGAMM", description = "", country = "RU", inputMuted = false, outputMuted = false, flagTalking = false } },
            new { id = 37L, channelId = "26", properties = new { nickname = "RKNcompany", description = "", country = "RU", inputMuted = false, outputMuted = false, flagTalking = true } }
        };

        var conn = new {
            channelInfos = new {
                rootChannels = new[] { root },
                subChannels = new Dictionary<string, object> { ["24"] = new[] { ch25, ch26 } }
            },
            clientId = 35L,
            clientInfos = clients,
            properties = new { name = "TERNION" }
        };

        var auth = new {
            payload = new {
                apiKey = "f6b1ff57-c902-4fd7-a1f5-d3356a96c248",
                connections = new[] { conn }
            },
            status = new { code = 0 },
            type = "auth"
        };
        return System.Text.Json.JsonSerializer.Serialize(auth);
    }

    public void Dispose() { _cts.Cancel(); try { _listener.Stop(); } catch { } }
}