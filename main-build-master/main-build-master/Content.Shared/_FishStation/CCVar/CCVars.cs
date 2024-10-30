using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._FishStation.CCVar;

// ReSharper disable once InconsistentNaming
[CVarDefs]
public sealed class CCVars
{
    /*
     * Discord Auth
     */

    /// <summary>
    ///     Enabled Discord linking, show linking button and modal window
    /// </summary>
    public static readonly CVarDef<bool> DiscordAuthEnabled =
        CVarDef.Create("discord_auth.enabled", true, CVar.SERVERONLY);

    /// <summary>
    ///     URL of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiUrl =
        CVarDef.Create("discord_auth.api_url", "https://localhost:3000", CVar.SERVERONLY);

    /// <summary>
    ///     Secret key of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiKey =
        CVarDef.Create("discord_auth.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /**
     * Sponsors
     */

    /// <summary>
    ///     URL of the sponsors server API.
    /// </summary>
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);

    public static readonly CVarDef<string> SponsorsSelectedGhost =
        CVarDef.Create("sponsor.ghost", "", CVar.CLIENTONLY);


    public static readonly CVarDef<bool>
        WhitelistRolesEnabled = CVarDef.Create("game.whitelist_role_enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool>
        NewServerAdvertisementEnabled = CVarDef.Create("game.advertise_new_server", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Хэш последнего дарова окна
    /// </summary>
    public static readonly CVarDef<bool> WelcomePopupLastSeen =
        CVarDef.Create("game.welcome_window", false, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> ThemidaEnabled =
        CVarDef.Create("game.enable_themida", true, CVar.SERVER );

}
