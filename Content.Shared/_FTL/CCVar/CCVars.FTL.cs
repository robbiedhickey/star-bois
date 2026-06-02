using Robust.Shared.Configuration;

namespace Content.Shared._FTL.CCVar;

[CVarDefs]
public sealed class CCVarsFTL
{
    /// <summary>
    ///     Whether or not to generate FTL points at round start.
    /// </summary>
    public static readonly CVarDef<bool> GenerateStarmapRoundstart =
        CVarDef.Create("starmap.generate_roundstart", false, CVar.ARCHIVE);

    /// <summary>
    ///     Which weighted random prototype to use for starmap generation.
    /// </summary>
    public static readonly CVarDef<string> StarmapRandomPrototypeId =
        CVarDef.Create("starmap.weighted_random_id", "DefaultStarmap", CVar.ARCHIVE);
}
