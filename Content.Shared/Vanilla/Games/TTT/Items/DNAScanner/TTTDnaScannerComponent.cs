namespace Content.Shared.Vanilla.Games.TTT.Items.DNAScanner;

[RegisterComponent]
public sealed partial class TTTDnaScannerComponent : Component
{

}
[RegisterComponent]
public sealed partial class TTTNoDnaComponent : Component
{

}
[RegisterComponent]
public sealed partial class OnDnaScannerComponent : Component
{
    [ViewVariables]
    public bool InStealth = false;
    [ViewVariables]
    public EntityUid? Redirect = null;

    [ViewVariables]
    public HashSet<EntityUid> Scanners = [];
}
[RegisterComponent]
public sealed partial class TTTDecoyComponent : Component
{
    [ViewVariables]
    public EntityUid? RedirectedBy = null;
}
