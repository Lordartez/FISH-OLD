using Content.Corvax.Interfaces.Shared;
using Robust.Client.Graphics;

namespace Content.Client._FishStation.DiscordAuth;

public interface IClientDiscordAuthManager : ISharedDiscordAuthManager
{
    public string AuthUrl { get; }
    public Texture? Qrcode { get; }
}
