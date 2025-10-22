using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Vanilla.SocialVerb;

/// <summary>
/// Прототип для социальных вербов.
/// </summary>
[Prototype("socialVerb")]
public sealed class SocialVerbPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField(required: true)] public string Text = string.Empty;
    [DataField] public string? Icon;
    [DataField] public bool RequiresActiveItem = false;
    [DataField] public bool RequiresInteractRange = false;
}
