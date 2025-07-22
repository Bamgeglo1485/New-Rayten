using Content.Shared.Vanilla.Dominator;
using System.Linq;

namespace Content.Server.Vanilla.Dominator;

public sealed class DangerMobSystem : SharedDangerMobSystem
{

    private float _timer = 0;
    public float CheckDelay = 0.5f;
    /// <summary>
    /// Вся обработка только на сервере, т.к. невозможно выстроить предикт
    /// </summary>
    /// <param name="frameTime"></param>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;

        if (_timer < CheckDelay)
            return;

        _timer = 0;

        var query = EntityQueryEnumerator<DangerMobComponent>();
        while (query.MoveNext(out var uid, out var mobdanger))
        {
            CalculateDanger(uid, mobdanger);
            Dirty(uid, mobdanger);
        }
    }
}
