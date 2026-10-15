namespace TernionCore;

/// <summary>Один участник на сервере TeamSpeak.</summary>
public sealed class Participant
{
    public long Id;
    public long ChannelId;
    public string Nickname = "";
    public string Description = "";
    public string AvatarUrl = "";
    public string Country = "";
    public string MyTsTag = "";
    public bool Talking;
    public bool InputMuted;
    public bool OutputMuted;
    public bool Away;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Description) ? Nickname : $"{Nickname} ({Description})";
}

/// <summary>Актуальное состояние: к какому каналу подключены, кто в нём, кто говорит.</summary>
public sealed class TeamState
{
    public bool Connected;
    public bool Authorized;
    public bool HasServerConnection;
    public string ServerName = "";
    public long OurClientId;
    public long OurChannelId;
    public string OurChannelName = "";
    public string Error = "";

    // Все участники (по id), в том числе на каналах вне нашего.
    public readonly Dictionary<long, Participant> All = new();

    // Каналы: id -> имя; ParentId: id -> id родителя (0 = корень).
    public readonly Dictionary<long, string> ChannelName = new();
    public readonly Dictionary<long, long> ChannelParent = new();

    /// <summary>Участники текущего голосового канала (что показываем в оверлее).</summary>
    public List<Participant> Visible => All.Values.Where(p => p.ChannelId == OurChannelId).OrderBy(p => p.Nickname).ToList();
}