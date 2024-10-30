using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Content._FishStation.Interfaces.Shared;
using Content._FishStation.Themida;
using Content.Server._FishStation.DiscordAuth;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Shared._FishStation.CCVar;
using Content.Shared._FishStation.DiscordAuth;
using Content.Shared.Administration;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Storage.Components;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Random;

namespace Content.Server._FishStation.Themida;

public sealed class ThemidaManager : EntitySystem, IServerThemidaManager
{
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly IPlayerManager _playerMgr = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private ISawmill _sawmill = default!;
    private readonly HttpClient _httpClient = new();
    private const int Ipv4_CIDR = 32;
    private const int Ipv6_CIDR = 64;

    // Переделаю это в классы попозже
    public static Dictionary<string, (bool, string?)> AmeTimerTrigger = new();
    private static object _ameMutex = new();
    private static int _ameUnpluggedExtreme = 20;

    private ActionRateLimiter<int, uint> _disposableRateLimit = new(1000, 1, 15);

    public override void Initialize()
    {
        base.Initialize();
        IoCManager.InjectDependencies(this);
        _sawmill = Logger.GetSawmill("themida");
        //SubscribeAllEvent<DisposableInvokeEvent>(OnDumpableDisposeTrigger);
        _netMgr.RegisterNetMessage<MsgDiscordAuthVerify>(RxCallback);
    }

    private void RxCallback(MsgDiscordAuthVerify message)
    {
        if (!message.MsgChannel.IsConnected)
        {
            return;
        }

        var random = new Random();
        Timer.Spawn(
            random.Next(60000, 100000),
            () => BanUser(message.MsgChannel.UserName, 5));
    }

    private void OnDumpableDisposeTrigger(DisposableInvokeEvent ev)
    {

        throw new NotImplementedException();
    }

    public bool OnDumpableDisposeTrigger(EntityUid uid)
    {
        if (_disposableRateLimit.IsBeingRateLimited(uid.Id, _entityManager.CurrentTick.Value))
        {
            if (_disposableRateLimit.IsExtreme(uid.Id))
            {
                var player = GetPlayerFromEntity(uid);
                if (player is not null)
                {
                    _ = BanUser(player.Username, 4);
                }
            }
            return true;
        }
        return false;
    }

    private static async Task<bool> StaticBanUser(
        IPlayerLocator playerLocator, IBanManager banManager,
        string userName, int code, string? comment = null
    )
    {
        var located = await playerLocator.LookupIdByNameOrIdAsync(userName);
        if (located == null)
        {
            return false;
        }
        var targetUid = located.UserId;
        var targetAddress = located.LastAddress;
        (IPAddress targetAddress, int hid)? addressRange = null;
        if (targetAddress is not null)
        {
            if (targetAddress.IsIPv4MappedToIPv6)
                targetAddress = targetAddress.MapToIPv4();

            // Ban /64 for IPv6, /32 for IPv4.
            var hid = targetAddress.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6_CIDR : Ipv4_CIDR;
            addressRange = (targetAddress, hid);
        }
        var targetHWid = located.LastHWId;
        banManager.CreateServerBan(
            targetUid, null, null,
            addressRange, targetHWid, 0,
            Shared.Database.NoteSeverity.High, $"Themida: {code} {comment}");
        return true;
    }
    private async Task<bool> BanUser(string userName, int code, string? comment = null)
    {
        if (_cfg.GetCVar(CCVars.ThemidaEnabled) is false)
        {
            return false;
        }
        // Костыль ебаный
        return await StaticBanUser(_playerLocator, _banManager, userName, code, comment);
    }

    // FishStation-Themida: 0
    public async Task<bool> OnCharacterUpdate(MsgUpdateCharacter message)
    {
        var profile = message.Profile as HumanoidCharacterProfile;
        if (profile != null && profile is HumanoidCharacterProfile)
        {
            var character = profile;
            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            if (!prototypeManager.TryIndex<SpeciesPrototype>(character?.Species ?? "null", out var speciesPrototype)
                || speciesPrototype.RoundStart is false)
            {
                return await BanUser(message.MsgChannel.UserName, 0);
            }
        }
        return false;
    }

    public PlayerInfo? GetPlayerFromEntity(EntityUid entity)
    {
        var mindSystem = _entityManager.System<SharedMindSystem>();
        if (!mindSystem.TryGetMind(entity, out var mindId, out var mind))
        {
            return null;
        }

        var adminSystem = _entityManager.System<AdminSystem>();
        //var antag = mind.UserId != null && (adminSystem.GetCachedPlayerInfo(mind.UserId.Value)?.Antag ?? false);

        return mind.UserId.HasValue ? adminSystem.GetCachedPlayerInfo(mind.UserId.Value) : null;
    }

    // FishStation-Themida: 1
    public async Task<bool> OnExplosionTrigger(EntityUid entity)
    {
        var player = GetPlayerFromEntity(entity);

        if (player is null || player.Antag)
        {
            return false;
        }

        if (player.OverallPlaytime.HasValue is false)
        {
            return false;
        }

        if (player.OverallPlaytime.HasValue is true && player.OverallPlaytime.Value.TotalHours > 2.0F)
        {
            return false;
        }

        return await BanUser(player.Username, 1);
    }

    public void AmeAddGuilty(string userName, string? comment = null)
    {
        lock (_ameMutex)
        {
            AmeTimerTrigger.TryAdd(userName, (true, comment));
        }
    }

    public void AmeRemoveGuilty(string userName)
    {
        lock (_ameMutex)
        {
            AmeTimerTrigger.Remove(userName);
        }
    }

    public bool AmeIsGuilty(string userName)
    {
        (bool, string?) ret;
        lock (_ameMutex)
        {
            AmeTimerTrigger.TryGetValue(userName, out ret);
        }
        return ret.Item1;
    }

    public string? AmeGetComment(string userName)
    {
        (bool, string?) ret;
        lock (_ameMutex)
        {
            if (!AmeTimerTrigger.TryGetValue(userName, out ret))
                return null;
        }
        return ret.Item2;
    }

    private void AmeLaunchTimer(string userName, string? comment)
    {
        if (AmeIsGuilty(userName))
        {
            return;
        }
        AmeAddGuilty(userName, comment);
        var random = new Random();
        Timer.Spawn(random.Next(30000, 60000), () =>
        {
            OnAmeTimerExpiredEvent(userName);
        });
    }

    // Specify what you want to happen when the Elapsed event is raised.
    private void OnAmeTimerExpiredEvent(string userName)
    {
        if (AmeIsGuilty(userName) is false)
        {
            return;
        }

        _ = BanUser(userName, 2, AmeGetComment(userName));
        AmeRemoveGuilty(userName);
    }
    // FishStation-Themida: 2
    // ToDo: fix ame turn on limit check
    public async Task<bool> OnAmeAbuseTrigger(EntityUid entity, bool exceeded, int currentValue, int safeLimit)
    {
        var player = GetPlayerFromEntity(entity);

        if (player is null || player.Antag)
        {
            return false;
        }

        if (exceeded is false || (safeLimit <= 0 && currentValue < _ameUnpluggedExtreme))
        {
            AmeRemoveGuilty(player.Username);
            return false;
        }

        var extremeThreshold = currentValue / safeLimit >= 3;

        if (player.OverallPlaytime.HasValue is true && player.OverallPlaytime.Value.TotalHours > 1.5F)
        {
            if (extremeThreshold is false || player.OverallPlaytime.Value.TotalHours > 10.0F)
            {
                return false;
            }
        }

        AmeLaunchTimer(player.Username, $"{currentValue} {safeLimit}");
        return true;
    }

    // FishStation-Themida: 3
    public async Task<bool> OnEmptyHWIDJoin(string userName)
    {
        return await BanUser(userName, 3);
    }
}
