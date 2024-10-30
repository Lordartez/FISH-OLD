using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Server._FishStation.Themida;
public sealed class ThemidaTriggerEvent : EntityEventArgs
{
    public int Code { get; }
    public NetUserId User { get; }

    public ThemidaTriggerEvent(NetUserId user, int code)
    {
        Code = code;
        User = user;
    }
}
