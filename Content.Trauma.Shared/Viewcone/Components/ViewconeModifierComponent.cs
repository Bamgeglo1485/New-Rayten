// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.GameStates;

namespace Content.Trauma.Shared.Viewcone.Components;

/// <summary>
/// Intended to be used on inventory items, mutations or status effects (i.e. this is relayed).
/// Modifies the viewcone angle of the relevant entity multiplicatively.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ViewconeModifierComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public float AngleModifier = 1f;
}
