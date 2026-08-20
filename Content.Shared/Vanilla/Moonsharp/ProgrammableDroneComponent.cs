
namespace Content.Shared.Vanilla.Moonsharp;

[RegisterComponent]
public sealed partial class ProgrammableDroneComponent : Component
{
    [DataField]
    public string Code = "";

    [DataField]
    public bool IsRunning = false;
}
