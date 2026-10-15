using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TernionCore;

public interface IKeyStore
{
    string? Load();
    void Save(string key);
}

/// <summary>
/// Клиент Remote Apps TeamSpeak 6: держит WebSocket, шлёт auth, разбирает события
/// (состав каналов/клиентов, talkStatusChanged, clientMoved, clientPropertiesUpdated)
/// и актуализирует TeamState.
/// </summary>
public sealed class Ts6Client : IAsyncDisposable
{
    public event Action? ConnectedChanged;   // вызвать в свободном от UI потоке
    public event Action? StateUpdated;
    public event Action<string>? Error;
    public event Action<string>? ConnectionFailed;

    public int Port => _port;

    public TeamState State { get; } = new();

    private const string Host = "127.0.0.1";
    private readonly int _port;
    private readonly string _identifier;
    private readonly IKeyStore _keyStore;
    private readonly byte[] _buffer = new byte[1_048_576];
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private string _apiKey = "";

    public Ts6Client(int port, IKeyStore keyStore, string identifier = "com.ternion.overlay")
    {
        _port = port;
        _identifier = identifier;
        _keyStore = keyStore;
        _apiKey = keyStore.Load() ?? "";
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            try { if (_ws is not null) await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch { }
            _cts.Dispose();
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            SetConnected(false);
            try
            {
                using var ws = new ClientWebSocket();
                _ws = ws;
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                await ws.ConnectAsync(new Uri($"ws://{Host}:{_port}"), linked.Token).ConfigureAwait(false);
                SetConnected(true);
                await SendAuthAsync(ws, linked.Token).ConfigureAwait(false);
                await ReadLoopAsync(ws, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                State.Error = FriendlyError(ex);
                Error?.Invoke(State.Error);
                ConnectionFailed?.Invoke(State.Error);
            }
            try { await Task.Delay(5000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private string FriendlyError(Exception ex)
    {
        var inner = ex.GetBaseException();
        if (inner is WebSocketException we)
        {
            if (we.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                return $"TeamSpeak закрыл соединение. Клиент запущен и подключён к серверу?";
            if (we.Message.Contains("10061", StringComparison.Ordinal) || inner.Message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
                return $"Не удалось подключиться к 127.0.0.1:{_port}. Проверьте: в TeamSpeak включён доступ (Настройки → Удаленные приложения) и порт совпадает.";
        }
        if (inner.Message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
            return $"Не удалось подключиться к 127.0.0.1:{_port}.";
        return inner.Message;
    }

    private void SetConnected(bool on)
    {
        State.Connected = on;
        if (on) State.Error = "";
        if (!on)
        {
            State.Authorized = false;
            State.HasServerConnection = false;
            State.All.Clear();
            State.ServerName = "";
            State.OurChannelName = "";
            State.OurClientId = 0;
            State.OurChannelId = 0;
        }
        ConnectedChanged?.Invoke();
        StateUpdated?.Invoke();
    }

    private async Task SendAuthAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var auth = new {
            type = "auth",
            payload = new {
                identifier = _identifier,
                version = "1.0.0",
                name = "Ternion Overlay",
                description = "Оверлей для TeamSpeak 6",
                content = new { apiKey = _apiKey }
            }
        };
        var json = JsonSerializer.Serialize(auth);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
    }

    private async Task ReadLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var pending = new StringBuilder();
        while (!ct.IsCancellationRequested)
        {
            pending.Clear();
            while (true)
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
                // The initial auth snapshot may have no server connections. Re-auth on
                // the same saved grant after a delay so joining a server updates the view.
                if (State.Authorized && !State.HasServerConnection)
                    idle.CancelAfter(TimeSpan.FromSeconds(10));
                WebSocketReceiveResult res;
                try { res = await ws.ReceiveAsync(new ArraySegment<byte>(_buffer), idle.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && State.Authorized && !State.HasServerConnection)
                { return; }
                if (res.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                }
                pending.Append(Encoding.UTF8.GetString(_buffer, 0, res.Count));
                if (res.EndOfMessage) break;
            }
            HandleMessage(pending.ToString(), ct);
        }
    }

    private void HandleMessage(string json, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        switch (type)
        {
            case "auth": HandleAuth(root); break;
            case "talkStatusChanged": HandleTalk(root, ct); break;
            case "clientMoved": HandleMoved(root); break;
            case "clientPropertiesUpdated": HandlePropsUpdated(root); break;
            case "proto.ClientEvent": HandleProto(root); break;
        }
    }

    private void HandleProto(JsonElement root)
    {
        // Дубликат некоторых событий: {payload:{talkStatusChanged:{clientId,status}}, type:"proto.ClientEvent"}
        if (root.TryGetProperty("payload", out var p) && p.TryGetProperty("talkStatusChanged", out var tsc))
        {
            long id = LongOf(tsc, "clientId");
            int status = tsc.TryGetProperty("status", out var s) ? s.GetInt32() : int.MinValue;
            ApplyTalk(id, status);
        }
    }

    private void HandleTalk(JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("payload", out var p)) return;
        long id = p.TryGetProperty("clientId", out var c) ? c.GetInt64() : 0;
        int status = p.TryGetProperty("status", out var s) ? s.GetInt32() : 0;
        ApplyTalk(id, status);
        StateUpdated?.Invoke();
    }

    private void ApplyTalk(long clientId, int status)
    {
        if (!State.All.TryGetValue(clientId, out var part)) return;
        part.Talking = status == 1; // 1 = начал говорить, 0/2 = прекратил
    }

    private void HandleAuth(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("payload", out var payload)) return;
            if (root.TryGetProperty("status", out var status) &&
                status.TryGetProperty("code", out var code) && code.GetInt32() != 0)
            {
                State.Error = "Remote Apps authorization rejected";
                StateUpdated?.Invoke();
                return;
            }
            if (payload.TryGetProperty("apiKey", out var keyEl) && keyEl.GetString() is string key && key.Length > 0 && key != _apiKey)
            {
                _apiKey = key;
                _keyStore.Save(key);
            }
            State.Authorized = true;
            State.HasServerConnection = payload.TryGetProperty("connections", out var conns)
                && conns.ValueKind == JsonValueKind.Array && conns.GetArrayLength() > 0;
            if (!State.HasServerConnection) { StateUpdated?.Invoke(); return; }

            // Выбираем соединение с самим собой (там clientId сверху) либо первое с клиентами.
            JsonElement conn = conns[0];
            long ourId = LongOf(conn, "clientId");

            // Карта каналов
            State.ChannelName.Clear();
            State.ChannelParent.Clear();
            if (conn.TryGetProperty("channelInfos", out var chInfos))
            {
                if (chInfos.TryGetProperty("rootChannels", out var roots))
                    foreach (var r in roots.EnumerateArray()) AddChannel(r, 0);
                if (chInfos.TryGetProperty("subChannels", out var subs))
                    foreach (var kv in subs.EnumerateObject())
                        foreach (var s in kv.Value.EnumerateArray())
                            AddChannel(s, long.TryParse(kv.Name, out var pp) ? pp : 0);
            }

            State.All.Clear();
            if (conn.TryGetProperty("clientInfos", out var infos))
            {
                foreach (var info in infos.EnumerateArray())
                {
                    var part = ParseClient(info, ourId);
                    if (part is not null) State.All[part.Id] = part;
                }
            }

            State.OurClientId = ourId;
            // Наш канал — канал клиента ourId
            if (State.All.TryGetValue(ourId, out var me))
            {
                State.OurChannelId = me.ChannelId;
                State.OurChannelName = NameOf(me.ChannelId);
            }
            if (conn.TryGetProperty("properties", out var sp) && sp.TryGetProperty("name", out var sn))
                State.ServerName = sn.GetString() ?? "";
            if (conn.TryGetProperty("properties", out var sp2) && sp2.TryGetProperty("welcomeMessage", out var wm))
                State.ServerName = State.ServerName + "";

            StateUpdated?.Invoke();
        }
        catch { }
    }

    private void AddChannel(JsonElement ch, long parentId)
    {
        try
        {
            long id = LongOf(ch, "id");
            string name = "";
            if (ch.TryGetProperty("properties", out var pr) && pr.TryGetProperty("name", out var n))
                name = n.GetString() ?? "";
            if (id <= 0) return;
            State.ChannelName[id] = name;
            State.ChannelParent[id] = parentId;
        }
        catch { }
    }

    private string NameOf(long channelId) =>
        State.ChannelName.TryGetValue(channelId, out var n) ? n : $"(канал {channelId})";

    private Participant? ParseClient(JsonElement info, long ownId)
    {
        try
        {
            long id = info.TryGetProperty("id", out var i) ? VarLong(i) : 0;
            long channelId = LongOf(info, "channelId");
            var p = new Participant { Id = id, ChannelId = channelId };
            if (info.TryGetProperty("properties", out var pr))
            {
                p.Nickname = Get(pr, "nickname");
                p.Description = Get(pr, "description");
                p.Country = Get(pr, "country");
                p.InputMuted = GetBool(pr, "inputMuted");
                p.OutputMuted = GetBool(pr, "outputMuted");
                p.Away = GetBool(pr, "away");
                p.Talking = GetBool(pr, "flagTalking");
                p.AvatarUrl = ParseAvatar(Get(pr, "myteamspeakAvatar"));
                if (pr.TryGetProperty("userTag", out var ut))
                    p.MyTsTag = ParseTag(ut);
            }
            return p;
        }
        catch { return null; }
    }

    private static string ParseAvatar(string raw)
    {
        // формат "2,https://..."
        var idx = raw.IndexOf(',');
        if (idx >= 0 && raw.Length > idx + 1) return raw[(idx + 1)..];
        return raw;
    }

    private static string ParseTag(JsonElement userTagEl)
    {
        try
        {
            if (userTagEl.ValueKind == JsonValueKind.String && userTagEl.GetString() is string s)
            {
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.TryGetProperty("tag", out var tag))
                    return tag.GetString() ?? "";
            }
        }
        catch { }
        return "";
    }

    private void HandleMoved(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("payload", out var p)) return;
            long id = p.TryGetProperty("clientId", out var c) ? c.GetInt64() : 0;
            long oldCh = LongOf(p, "oldChannelId");
            long newCh = LongOf(p, "newChannelId");
            if (State.All.TryGetValue(id, out var part))
            {
                part.ChannelId = newCh;
                if (id == State.OurClientId)
                {
                    State.OurChannelId = newCh;
                    State.OurChannelName = NameOf(newCh);
                }
                StateUpdated?.Invoke();
            }
        }
        catch { }
    }

    private void HandlePropsUpdated(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("payload", out var p)) return;
            long id = LongOf(p, "clientId");
            if (!State.All.TryGetValue(id, out var part)) return;
            if (p.TryGetProperty("properties", out var pr))
            {
                part.InputMuted = GetBool(pr, "inputMuted");
                part.OutputMuted = GetBool(pr, "outputMuted");
                part.Away = GetBool(pr, "away");
                var nick = Get(pr, "nickname");
                if (nick.Length > 0) part.Nickname = nick;
                if (pr.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
                    part.Description = description.GetString() ?? "";
                StateUpdated?.Invoke();
            }
        }
        catch { }
    }

    private static string Get(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    // Поле может прийти числом (id клиента) или строкой ("25" — id канала).
    private static long LongOf(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) ? VarLong(v) : 0;

    private static long VarLong(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number => v.TryGetInt64(out var n) ? n : 0,
        JsonValueKind.String => long.TryParse(v.GetString(), out var s) ? s : 0,
        _ => 0
    };

    private static bool GetBool(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}