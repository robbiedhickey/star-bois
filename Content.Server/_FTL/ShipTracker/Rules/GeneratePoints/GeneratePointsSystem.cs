using System.Linq;
using System.Numerics;
using Content.Server._FTL.FTLPoints.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Shared.Station.Components;
using Content.Server.Station.Systems;
using Robust.Shared.Configuration;
using Content.Shared._FTL.CCVar;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._FTL.ShipTracker.Rules.GeneratePoints;

/// <summary>
/// Generates points roundstart, see <see cref="GeneratePointsComponent"/>.
/// </summary>
public sealed class GeneratePointsSystem : GameRuleSystem<GeneratePointsComponent>
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly FtlPointsSystem _pointsSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void Started(EntityUid uid, GeneratePointsComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.Generated)
            return;

        if (!_configurationManager.GetCVar(CCVarsFTL.GenerateStarmapRoundstart))
            return;

        var sector = _pointsSystem.GenerateSector(25, null, false, false);

        // Move the ship grid onto the sector map at a random starting position.
        foreach (var stationUid in _stationSystem.GetStations())
        {
            if (!TryComp<StationDataComponent>(stationUid, out var stationData))
                continue;

            var grid = _stationSystem.GetLargestGrid(new Entity<StationDataComponent?>(stationUid, stationData));
            if (!grid.HasValue)
                continue;

            EnsureComp<ShuttleComponent>(grid.Value);
            _mapManager.SetMapPaused(sector, false);
            _transformSystem.SetCoordinates(grid.Value,
                new EntityCoordinates(_mapManager.GetMapEntityId(sector),
                    new Vector2(
                        _pointsSystem.GenerateVectorWithRandomRadius(100, 600),
                        _pointsSystem.GenerateVectorWithRandomRadius(100, 600))));
            break;
        }

        component.Generated = true;
        Log.Info("Finished generation of sector.");
    }
}
