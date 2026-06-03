using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._FTL.FTLPoints.Effects;

[DataDefinition]
public sealed partial class SpawnMapEffect : FtlPointEffect
{
    [DataField("mapPaths", required: true)]
    public List<ResPath> MapPaths { set; get; } = new List<ResPath>()
    {
        new ResPath("/Maps/_FTL/trade-station.yml")
    };

    public override void Effect(FtlPointEffectArgs args)
    {
        var mapLoader = args.EntityManager.System<MapLoaderSystem>();
        var random = IoCManager.Resolve<IRobustRandom>();
        try
        {
            mapLoader.TryLoadGrid(args.MapId, random.Pick(MapPaths), out _);
        }
        catch (Exception ex)
        {
            Logger.ErrorS("ftl.spawn_map", $"Failed to load map into star: {ex.Message}");
        }
    }
}
