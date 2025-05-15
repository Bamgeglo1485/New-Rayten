using Robust.Shared.Serialization;
using System.Linq;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Vanilla.Background;

public sealed class SharedBackgroundSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }

    public static string GetJobPrototype(string? background)
    {
        if (string.IsNullOrEmpty(background))
            return string.Empty;
        return "Job" + background;
    }

}

[Serializable, NetSerializable]
public sealed class TakeGhostBackgroundEvent : EntityEventArgs
{
    public readonly ProtoId<BackgroundPrototype> Background; 
    public TakeGhostBackgroundEvent(ProtoId<BackgroundPrototype> background)
    {
        Background = background;
    }
}