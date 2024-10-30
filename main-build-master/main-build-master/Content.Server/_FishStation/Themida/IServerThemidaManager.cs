using System.Threading;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Shared;
using Content._FishStation.Interfaces.Shared;
using Content.Server._FishStation.DiscordAuth;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Preferences;
using Content.Shared.Storage.Components;

namespace Content._FishStation.Themida;

public interface IServerThemidaManager : ISharedThemidaManager
{
    public Task<bool> OnEmptyHWIDJoin(string userName);
    public Task<bool> OnCharacterUpdate(MsgUpdateCharacter message);
    public Task<bool> OnExplosionTrigger(EntityUid entity);
    public Task<bool> OnAmeAbuseTrigger(EntityUid entity, bool exceeded, int currentValue, int safeLimit);
}
