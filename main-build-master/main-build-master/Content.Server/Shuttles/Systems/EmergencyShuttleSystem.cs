using System.Numerics;
using System.Threading;
using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.GameTicking.Events;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Server.Screens.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.GameTicking;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Tag;
using Content.Shared.Tiles;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem : EntitySystem
{
    /*
     * Handles the escape shuttle + CentCom.
     */

    [Dependency] private readonly IAdminLogManager _logger = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AccessReaderSystem _reader = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly CommunicationsConsoleSystem _commsConsole = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly DockingSystem _dock = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IdCardSystem _idSystem = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly Content.Server.Backmen.Arrivals.CentcommSystem _centcommSystem = default!;


    private const float ShuttleSpawnBuffer = 1f;

    private bool _emergencyShuttleEnabled;

    [ValidatePrototypeId<TagPrototype>]
    private const string DockTag = "DockEmergency";

    public override void Initialize()
    {
        _emergencyShuttleEnabled = _configManager.GetCVar(CCVars.EmergencyShuttleEnabled);
        // Don't immediately invoke as roundstart will just handle it.

        //Subs.CVar(_configManager, CCVars.EmergencyShuttleEnabled, SetEmergencyShuttleEnabled);
        _configManager.OnValueChanged(CCVars.EmergencyShuttleEnabled, value => SetEmergencyShuttleEnabled(value), true);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<StationEmergencyShuttleComponent, ComponentStartup>(OnStationStartup);
        //SubscribeLocalEvent<StationCentcommComponent, ComponentShutdown>(OnCentcommShutdown); // backmen: centcom
        SubscribeLocalEvent<StationCentcommComponent, ComponentInit>(OnCentcommInit);

        SubscribeLocalEvent<EmergencyShuttleComponent, FTLStartedEvent>(OnEmergencyFTL);
        SubscribeLocalEvent<EmergencyShuttleComponent, FTLCompletedEvent>(OnEmergencyFTLComplete);
        SubscribeNetworkEvent<EmergencyShuttleRequestPositionMessage>(OnShuttleRequestPosition);
        InitializeEmergencyConsole();
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        CleanupEmergencyConsole();
        _roundEndCancelToken = new CancellationTokenSource();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _roundEndCancelToken?.Cancel();
        _roundEndCancelToken = null;
    }

    private void OnCentcommShutdown(EntityUid uid, StationCentcommComponent component, ComponentShutdown args)
    {
        ClearCentcomm(component);
    }

    private void ClearCentcomm(StationCentcommComponent component)
    {
        QueueDel(component.Entity);
        QueueDel(component.MapEntity);
        component.Entity = null;
        component.MapEntity = null;
    }

    /// <summary>
    ///     Attempts to get the EntityUid of the emergency shuttle
    /// </summary>
    public EntityUid? GetShuttle()
    {
        AllEntityQuery<EmergencyShuttleComponent>().MoveNext(out var shuttle, out _);
        return shuttle;
    }

    private void SetEmergencyShuttleEnabled(bool value)
    {
        if (_emergencyShuttleEnabled == value)
            return;

        _emergencyShuttleEnabled = value;

        if (value)
        {
            SetupEmergencyShuttle();
        }
        else
        {
            CleanupEmergencyShuttle();
        }
    }

    private void CleanupEmergencyShuttle()
    {
        var query = AllEntityQuery<StationCentcommComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            RemCompDeferred<StationCentcommComponent>(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateEmergencyConsole(frameTime);
    }

    /// <summary>
    ///     If the client is requesting debug info on where an emergency shuttle would dock.
    /// </summary>
    private void OnShuttleRequestPosition(EmergencyShuttleRequestPositionMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.IsAdmin(args.SenderSession))
            return;

        var player = args.SenderSession.AttachedEntity;
        if (player is null)
            return;

        var station = _station.GetOwningStation(player.Value);

        if (!TryComp<StationEmergencyShuttleComponent>(station, out var stationShuttle) ||
            !HasComp<ShuttleComponent>(stationShuttle.EmergencyShuttle))
        {
            return;
        }

        var targetGrid = _station.GetLargestGrid(Comp<StationDataComponent>(station.Value));
        if (targetGrid == null)
            return;

        var config = _dock.GetDockingConfig(stationShuttle.EmergencyShuttle.Value, targetGrid.Value, DockTag);
        if (config == null)
            return;

        RaiseNetworkEvent(new EmergencyShuttlePositionMessage()
        {
            StationUid = GetNetEntity(targetGrid),
            Position = config.Area,
        });
    }

    /// <summary>
    ///     Escape shuttle FTL event handler. The only escape shuttle FTL transit should be from station to centcomm at round end
    /// </summary>
    private void OnEmergencyFTL(EntityUid uid, EmergencyShuttleComponent component, ref FTLStartedEvent args)
    {
        TimeSpan ftlTime = TimeSpan.FromSeconds
        (
            TryComp<FTLComponent>(uid, out var ftlComp) ?
            ftlComp.TravelTime : ShuttleSystem.DefaultTravelTime
        );

        if (TryComp<DeviceNetworkComponent>(uid, out var netComp))
        {
            var payload = new NetworkPayload
            {
                [ShuttleTimerMasks.ShuttleMap] = uid,
                [ShuttleTimerMasks.SourceMap] = args.FromMapUid,
                [ShuttleTimerMasks.DestMap] = args.TargetCoordinates.GetMapUid(_entityManager),
                [ShuttleTimerMasks.ShuttleTime] = ftlTime,
                [ShuttleTimerMasks.SourceTime] = ftlTime,
                [ShuttleTimerMasks.DestTime] = ftlTime
            };
            _deviceNetworkSystem.QueuePacket(uid, null, payload, netComp.TransmitFrequency);
        }
    }

    /// <summary>
    ///     When the escape shuttle finishes FTL (docks at centcomm), have the timers display the round end countdown
    /// </summary>
    private void OnEmergencyFTLComplete(EntityUid uid, EmergencyShuttleComponent component, ref FTLCompletedEvent args)
    {
        var countdownTime = TimeSpan.FromSeconds(_configManager.GetCVar(CCVars.RoundRestartTime));
        var shuttle = args.Entity;
        if (TryComp<DeviceNetworkComponent>(shuttle, out var net))
        {
            var payload = new NetworkPayload
            {
                [ShuttleTimerMasks.ShuttleMap] = shuttle,
                [ShuttleTimerMasks.SourceMap] = _roundEnd.GetCentcomm(),
                [ShuttleTimerMasks.DestMap] = _roundEnd.GetStation(),
                [ShuttleTimerMasks.ShuttleTime] = countdownTime,
                [ShuttleTimerMasks.SourceTime] = countdownTime,
                [ShuttleTimerMasks.DestTime] = countdownTime,
            };

            // by popular request
            // https://discord.com/channels/310555209753690112/770682801607278632/1189989482234126356
            if (_random.Next(1000) == 0)
            {
                payload.Add(ScreenMasks.Text, ShuttleTimerMasks.Kill);
                payload.Add(ScreenMasks.Color, Color.Red);
            }
            else
                payload.Add(ScreenMasks.Text, ShuttleTimerMasks.Bye);

            _deviceNetworkSystem.QueuePacket(shuttle, null, payload, net.TransmitFrequency);
        }
    }

    /// <summary>
    ///     Attempts to dock the emergency shuttle to the station.
    /// </summary>
    public void CallEmergencyShuttle(EntityUid stationUid, StationEmergencyShuttleComponent? stationShuttle = null)
    {
        if (!Resolve(stationUid, ref stationShuttle) ||
            !TryComp<TransformComponent>(stationShuttle.EmergencyShuttle, out var xform) ||
            !TryComp<ShuttleComponent>(stationShuttle.EmergencyShuttle, out var shuttle))
        {
            return;
        }

        var targetGrid = _station.GetLargestGrid(Comp<StationDataComponent>(stationUid));

        // UHH GOOD LUCK
        if (targetGrid == null)
        {
            _logger.Add(LogType.EmergencyShuttle, LogImpact.High, $"Emergency shuttle {ToPrettyString(stationUid)} unable to dock with station {ToPrettyString(stationUid)}");
            _chatSystem.DispatchStationAnnouncement(stationUid, Loc.GetString("emergency-shuttle-good-luck"), playDefaultSound: false);
            // TODO: Need filter extensions or something don't blame me.
            _audio.PlayGlobal("/Audio/Misc/notice1.ogg", Filter.Broadcast(), true);
            return;
        }

        var xformQuery = GetEntityQuery<TransformComponent>();

        if (_shuttle.TryFTLDock(stationShuttle.EmergencyShuttle.Value, shuttle, targetGrid.Value, DockTag))
        {
            if (TryComp<TransformComponent>(targetGrid.Value, out var targetXform))
            {
                var angle = _dock.GetAngle(stationShuttle.EmergencyShuttle.Value, xform, targetGrid.Value, targetXform, xformQuery);
                _chatSystem.DispatchStationAnnouncement(stationUid, Loc.GetString("emergency-shuttle-docked", ("time", $"{_consoleAccumulator:0}"), ("direction", Loc.GetString($"direction-ui-{angle.GetDir().ToString().ToLowerInvariant()}"))), playDefaultSound: false);
            }

            // shuttle timers
            var time = TimeSpan.FromSeconds(_consoleAccumulator);
            if (TryComp<DeviceNetworkComponent>(stationShuttle.EmergencyShuttle.Value, out var netComp))
            {
                var payload = new NetworkPayload
                {
                    [ShuttleTimerMasks.ShuttleMap] = stationShuttle.EmergencyShuttle.Value,
                    [ShuttleTimerMasks.SourceMap] = targetXform?.MapUid,
                    [ShuttleTimerMasks.DestMap] = _roundEnd.GetCentcomm(),
                    [ShuttleTimerMasks.ShuttleTime] = time,
                    [ShuttleTimerMasks.SourceTime] = time,
                    [ShuttleTimerMasks.DestTime] = time + TimeSpan.FromSeconds(TransitTime),
                    [ShuttleTimerMasks.Docked] = true
                };
                _deviceNetworkSystem.QueuePacket(stationShuttle.EmergencyShuttle.Value, null, payload, netComp.TransmitFrequency);
            }

            _logger.Add(LogType.EmergencyShuttle, LogImpact.High, $"Emergency shuttle {ToPrettyString(stationUid)} docked with stations");
            // TODO: Need filter extensions or something don't blame me.
            _audio.PlayGlobal("/Audio/Announcements/shuttle_dock.ogg", Filter.Broadcast(), true);
        }
        else
        {
            var location = FormattedMessage.RemoveMarkup(_navMap.GetNearestBeaconString((stationShuttle.EmergencyShuttle.Value, xform)));
            _chatSystem.DispatchStationAnnouncement(stationUid, Loc.GetString("emergency-shuttle-nearby", ("direction", location)), playDefaultSound: false);

            _logger.Add(LogType.EmergencyShuttle, LogImpact.High, $"Emergency shuttle {ToPrettyString(stationUid)} unable to find a valid docking port for {ToPrettyString(stationUid)}");
            // TODO: Need filter extensions or something don't blame me.
            _audio.PlayGlobal("/Audio/Misc/notice1.ogg", Filter.Broadcast(), true);
        }
    }

    private void OnCentcommInit(EntityUid uid, StationCentcommComponent component, ComponentInit args)
    {
        if (!_emergencyShuttleEnabled)
            return;

        // Post mapinit? fancy
        if (TryComp<TransformComponent>(component.Entity, out var xform))
        {
            component.MapEntity = xform.MapUid;
            return;
        }

        AddCentcomm(component);
    }

    private void OnStationStartup(EntityUid uid, StationEmergencyShuttleComponent component, ComponentStartup args)
    {
        AddEmergencyShuttle(uid, component);
    }

    /// <summary>
    ///     Spawns the emergency shuttle for each station and starts the countdown until controls unlock.
    /// </summary>
    public void CallEmergencyShuttle()
    {
        if (EmergencyShuttleArrived)
            return;

        if (!_emergencyShuttleEnabled)
        {
            _roundEnd.EndRound();
            return;
        }

        _consoleAccumulator = _configManager.GetCVar(CCVars.EmergencyShuttleDockTime);
        EmergencyShuttleArrived = true;

        var query = AllEntityQuery<StationEmergencyShuttleComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            CallEmergencyShuttle(uid, comp);
        }

        _commsConsole.UpdateCommsConsoleInterface();
    }

    private void SetupEmergencyShuttle()
    {
        if (!_emergencyShuttleEnabled)
            return;

        var centcommQuery = AllEntityQuery<StationCentcommComponent>();

        while (centcommQuery.MoveNext(out var centcomm))
        {
            AddCentcomm(centcomm);
        }

        var query = AllEntityQuery<StationEmergencyShuttleComponent>();

        while (query.MoveNext(out var uid, out var comp))
            AddEmergencyShuttle(uid, comp);
    }

// start-backmen: centcom
    private void AddCentcomm(StationCentcommComponent component)
    {
        var centcom = EntityManager.System<Content.Server.Backmen.Arrivals.CentcommSystem>();

        centcom.EnsureCentcom(true);
        component.MapEntity = centcom.CentComMapUid;
        component.Entity = centcom.CentComGrid;
        component.ShuttleIndex = centcom.ShuttleIndex;
    }

    public HashSet<EntityUid> GetCentcommMaps()
    {
        var maps = new HashSet<EntityUid>();

        if(_centcommSystem.CentComMapUid.IsValid())
            maps.Add(_centcommSystem.CentComMapUid);

        return maps;
    }
// end-backmen: centcom

    private void AddEmergencyShuttle(EntityUid uid, StationEmergencyShuttleComponent component)
    {
        if (!_emergencyShuttleEnabled
            || component.EmergencyShuttle != null ||
            !TryComp<StationCentcommComponent>(uid, out var centcomm)
            || !TryComp(centcomm.MapEntity, out MapComponent? map))
        {
            return;
        }

        // Load escape shuttle
        var shuttlePath = component.EmergencyShuttlePath;
        var shuttle = _map.LoadGrid(map.MapId, shuttlePath.ToString(), new MapLoadOptions()
        {
            // Should be far enough... right? I'm too lazy to bounds check CentCom rn.
            Offset = new Vector2(500f + centcomm.ShuttleIndex, 0f),
            // fun fact: if you just fucking yeet centcomm into nullspace anytime you try to spawn the shuttle, then any distance is far enough. so lets not do that
            LoadMap = false,
        });

        if (shuttle == null)
        {
            Log.Error($"Unable to spawn emergency shuttle {shuttlePath} for {ToPrettyString(uid)}");
            return;
        }

        centcomm.ShuttleIndex = _centcommSystem.ShuttleIndex;
        _centcommSystem.ShuttleIndex += _mapManager.GetGrid(shuttle.Value).LocalAABB.Width + ShuttleSpawnBuffer;

        component.EmergencyShuttle = shuttle;
        EnsureComp<ProtectedGridComponent>(shuttle.Value);
        EnsureComp<PreventPilotComponent>(shuttle.Value);
        EnsureComp<EmergencyShuttleComponent>(shuttle.Value);
    }

    /// <summary>
    /// Returns whether a target is escaping on the emergency shuttle, but only if evac has arrived.
    /// </summary>
    public bool IsTargetEscaping(EntityUid target)
    {
        // if evac isn't here then sitting in a pod doesn't return true
        if (!EmergencyShuttleArrived)
            return false;

        // check each emergency shuttle
        var xform = Transform(target);
        foreach (var stationData in EntityQuery<StationEmergencyShuttleComponent>())
        {
            if (stationData.EmergencyShuttle == null)
                continue;

            if (IsOnGrid(xform, stationData.EmergencyShuttle.Value))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOnGrid(TransformComponent xform, EntityUid shuttle, MapGridComponent? grid = null, TransformComponent? shuttleXform = null)
    {
        if (!Resolve(shuttle, ref grid, ref shuttleXform))
            return false;

        return _transformSystem.GetWorldMatrix(shuttleXform).TransformBox(grid.LocalAABB).Contains(_transformSystem.GetWorldPosition(xform));
    }
}
