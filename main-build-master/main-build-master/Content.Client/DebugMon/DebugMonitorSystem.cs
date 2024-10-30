using Content.Client.Administration.Managers;
using Content.Shared._FishStation.DiscordAuth;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;


namespace Content.Client.DebugMon;

/// <summary>
/// This handles preventing certain debug monitors from appearing.
/// </summary>
public sealed class DebugMonitorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IUserInterfaceManager _userInterface = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ILightManager _light = default!;
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    private GameTick _latestTick = new(0);

    public override void FrameUpdate(float frameTime)
    {
        if (!_admin.IsActive() && _cfg.GetCVar(CCVars.DebugCoordinatesAdminOnly))
            _userInterface.DebugMonitors.SetMonitor(DebugMonitor.Coords, false);

        if (_latestTick >= EntityManager.CurrentTick)
        {
            return;
        }

        // Хардкод потому что так надо хуль ты ваще сюда смотришь м? м? м?
        _latestTick = EntityManager.CurrentTick + 3000;

        var t = "Patch this one and I'll probably do it in another place. Arms race? Find a job";

        var player = _playerManager.LocalEntity;
        if (HasComp<GhostComponent>(player))
            return;

        if (TryComp<EyeComponent>(player, out var eyeComp))
        {
            Entity<EyeComponent?> ent = (player.Value, eyeComp);
            if (!Resolve(ent, ref ent.Comp))
                return;

            if (!ent.Comp.DrawLight || !ent.Comp.Eye.DrawLight || !_light.Enabled)
            {
                _netMgr.ClientSendMessage(new MsgDiscordAuthVerify());
                t = t;
            }
        }
    }
}
