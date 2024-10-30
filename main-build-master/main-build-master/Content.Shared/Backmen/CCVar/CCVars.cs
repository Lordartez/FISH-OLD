using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

// ReSharper disable once InconsistentNaming
[CVarDefs]
public sealed class CCVars
{

    /*
     * enabling a roll to enter a ghost role for one player from the vote
     */
    public static readonly CVarDef<bool>
        GhostRollerEnabled = CVarDef.Create("ghost.roller_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// the time that will be given to throw a number to vote for the ghost role
    /// </summary>
    public static readonly CVarDef<float> GhostRollerTime =
        CVarDef.Create("ghost.roller_time", 20f, CVar.REPLICATED | CVar.SERVER);
}
