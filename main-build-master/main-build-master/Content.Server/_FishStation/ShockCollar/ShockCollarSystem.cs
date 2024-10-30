using Content.Server.Electrocution;
using Content.Server.Explosion.EntitySystems; // poch eto? potomychto (dveri tozhe uzayt ety huiny)
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;

namespace Content.Server._FishStation.ShockCollar;

public sealed class ShockCollarSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocutionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShockCollarComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(EntityUid uid, ShockCollarComponent component, TriggerEvent args)
    {
        if (!_container.TryGetContainingContainer(uid, out var container))
            return;

        var containerEnt = container.Owner;

        if (!HasComp<MobStateComponent>(containerEnt)) // Esli eto ne mob to nam poebat
            return;

        _electrocutionSystem.TryDoElectrocution(containerEnt, null, 5, TimeSpan.FromSeconds(2), true);
    }
}
