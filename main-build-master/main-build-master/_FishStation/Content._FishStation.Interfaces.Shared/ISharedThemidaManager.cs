using Robust.Shared.GameObjects;

namespace Content._FishStation.Interfaces.Shared;

public interface ISharedThemidaManager
{
    public void Initialize();
    public bool OnDumpableDisposeTrigger(EntityUid uid);
}

public sealed class DisposableInvokeEvent
{
    public DisposableInvokeEvent()
    {
    }
}
