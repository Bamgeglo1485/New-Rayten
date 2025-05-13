using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Random;
using Robust.Shared.Collections;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared.Preferences;

namespace Content.Shared.Vanilla.Background;


[Serializable, NetSerializable, DataDefinition]
public sealed partial class RoleBackground : IEquatable<RoleBackground>
{
    [DataField]
    public ProtoId<RoleBackgroundPrototype> Role;

    [DataField]
    public BackGround? SelectedBabyBackground;

    [DataField]
    public BackGround? SelectedAdultBackground;

    [DataField]
    public BackGround? SelectedGeneralBackground;

    public RoleBackground(ProtoId<RoleBackgroundPrototype> role)
    {
        Role = role;
    }

    public RoleBackground Clone()
    {
        return new RoleBackground(Role)
        {
            SelectedBabyBackground = SelectedBabyBackground,
            SelectedAdultBackground = SelectedAdultBackground,
            SelectedGeneralBackground = SelectedGeneralBackground
        };
    }


    /// <summary>
    /// Ensures all prototypes exist and effects can be applied.
    /// </summary>
    // public void EnsureValid(HumanoidCharacterProfile profile, ICommonSession session, IDependencyCollection collection)
    // {
        // var groupRemove = new ValueList<string>();
        // var protoManager = collection.Resolve<IPrototypeManager>();
        // var netManager = collection.Resolve<INetManager>(); // Corvax-Loadouts

        // if (!protoManager.TryIndex(Role, out var roleProto))
        // {
        //     EntityName = null;
        //     SelectedLoadouts.Clear();
        //     return;
        // }

        // // Remove name not allowed.
        // if (!roleProto.CanCustomizeName)
        // {
        //     EntityName = null;
        // }

        // // Validate name length
        // // TODO: Probably allow regex to be supplied?
        // if (EntityName != null)
        // {
        //     var name = EntityName.Trim();

        //     if (name.Length > HumanoidCharacterProfile.MaxNameLength)
        //     {
        //         EntityName = name[..HumanoidCharacterProfile.MaxNameLength];
        //     }

        //     if (name.Length == 0)
        //     {
        //         EntityName = null;
        //     }
        // }

        // // In some instances we might not have picked up a new group for existing data.
        // foreach (var groupProto in roleProto.Groups)
        // {
        //     if (SelectedLoadouts.ContainsKey(groupProto))
        //         continue;

        //     // Data will get set below.
        //     SelectedLoadouts[groupProto] = new List<Loadout>();
        // }

        // // Reset points to recalculate.
        // Points = roleProto.Points;

        // foreach (var (group, groupLoadouts) in SelectedLoadouts)
        // {
        //     // Check the group is even valid for this role.
        //     if (!roleProto.Groups.Contains(group))
        //     {
        //         groupRemove.Add(group);
        //         continue;
        //     }

        //     // Dump if Group doesn't exist
        //     if (!protoManager.TryIndex(group, out var groupProto))
        //     {
        //         groupRemove.Add(group);
        //         continue;
        //     }

        //     // Corvax-Loadouts-Start
        //     var groupProtoLoadouts = groupProto.Loadouts;
        //     if (collection.TryResolveType<ISharedLoadoutsManager>(out var loadoutsManager) && group.Id == "Inventory")
        //     {
        //         var prototypes = new List<string>();
        //         if (netManager.IsClient)
        //         {
        //             prototypes = loadoutsManager.GetClientPrototypes();
        //         }
        //         else if (loadoutsManager.TryGetServerPrototypes(session.UserId, out var protos))
        //         {
        //             prototypes = protos;
        //         }

        //         groupProtoLoadouts = prototypes.Select(id => (ProtoId<LoadoutPrototype>)id).ToList();
        //     }
        //     // Corvax-Loadouts-End

        //     var loadouts = groupLoadouts[..Math.Min(groupLoadouts.Count, groupProto.MaxLimit)];

        //     // Validate first
        //     for (var i = loadouts.Count - 1; i >= 0; i--)
        //     {
        //         var loadout = loadouts[i];

        //         // Old prototype or otherwise invalid.
        //         if (!protoManager.TryIndex(loadout.Prototype, out var loadoutProto))
        //         {
        //             loadouts.RemoveAt(i);
        //             continue;
        //         }

        //         // Malicious client maybe, check the group even has it.
        //         if (!groupProto.Loadouts.Contains(loadout.Prototype))
        //         {
        //             loadouts.RemoveAt(i);
        //             continue;
        //         }

        //         // Validate the loadout can be applied (e.g. points).
        //         if (!IsValid(profile, session, loadout.Prototype, collection, out _))
        //         {
        //             loadouts.RemoveAt(i);
        //             continue;
        //         }

        //         Apply(loadoutProto);
        //     }

        //     // Apply defaults if required
        //     // Technically it's possible for someone to game themselves into loadouts they shouldn't have
        //     // If you put invalid ones first but that's your fault for not using sensible defaults
        //     if (loadouts.Count < groupProto.MinLimit)
        //     {
        //         foreach (var protoId in groupProtoLoadouts) // Corvax-Loadout: Use groupProtoLoadouts instead of groupProto.Loadouts
        //         {
        //             if (loadouts.Count >= groupProto.MinLimit)
        //                 break;

        //             if (!protoManager.TryIndex(protoId, out var loadoutProto))
        //                 continue;

        //             var defaultLoadout = new Loadout()
        //             {
        //                 Prototype = loadoutProto.ID,
        //             };

        //             if (loadouts.Contains(defaultLoadout))
        //                 continue;

        //             // Not valid so don't default to it anyway.
        //             if (!IsValid(profile, session, defaultLoadout.Prototype, collection, out _))
        //                 continue;

        //             loadouts.Add(defaultLoadout);
        //             Apply(loadoutProto);
        //         }
        //     }

        //     SelectedLoadouts[group] = loadouts;
        // }

        // foreach (var value in groupRemove)
        // {
        //     SelectedLoadouts.Remove(value);
        // }
    // }

    public void SetDefault(HumanoidCharacterProfile? profile, ICommonSession? session, IPrototypeManager protoManager, bool force = false)
    {
        if (profile == null)
            return;

        var collection = IoCManager.Instance!;
        var roleProto = protoManager.Index(Role);

        if (force)
        {
            SelectedBabyBackground = null;
            SelectedAdultBackground = null;
            SelectedGeneralBackground = null;
        }
    }


    // /// <summary>
    // /// Returns whether a loadout is valid or not.
    // /// </summary>
    public bool IsValid(HumanoidCharacterProfile profile, ICommonSession? session, ProtoId<BackgroundPrototype> background, IDependencyCollection collection, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        var protoManager = collection.Resolve<IPrototypeManager>();

        if (!protoManager.TryIndex(background, out var backgroundProto))
        {
            // Uhh
            reason = FormattedMessage.FromMarkupOrThrow("");
            return false;
        }

        if (!protoManager.HasIndex(Role))
        {
            reason = FormattedMessage.FromUnformatted("backgrounds-prototype-missing");
            return false;
        }

        var valid = true;

        // foreach (var effect in loadoutProto.Effects)
        // {
        //     valid = valid && effect.Validate(profile, this, loadoutProto, session, collection, out reason);
        // }

        return valid;
    }

    public bool AddBackground(ProtoId<BackgroundPrototype> selectedBackground, ProtoId<BackgroundGroupPrototype> selectedGroup, IPrototypeManager protoManager)
    {
        if (!protoManager.TryIndex(selectedGroup, out var groupProto))
            return false;

        if (!groupProto.Backgrounds.Contains(selectedBackground))
            return false;

        var background = new BackGround
        {
            Prototype = selectedBackground
        };

        switch (groupProto.Type)
        {
            case BackgroundGroupType.Baby:
                SelectedBabyBackground = background;
                break;
            case BackgroundGroupType.Adult:
                SelectedAdultBackground = background;
                break;
            case BackgroundGroupType.General:
                SelectedGeneralBackground = background;
                break;
            default:
                return false;
        }

        return true;
    }

    public bool RemoveBackground(ProtoId<BackgroundGroupPrototype> selectedGroup, IPrototypeManager protoManager)
    {
        if (!protoManager.TryIndex(selectedGroup, out var groupProto))
            return false;

        switch (groupProto.Type)
        {
            case BackgroundGroupType.Baby:
                if (SelectedBabyBackground == null)
                    return false;

                SelectedBabyBackground = null;
                return true;

            case BackgroundGroupType.Adult:
                if (SelectedAdultBackground == null)
                    return false;

                SelectedAdultBackground = null;
                return true;

            case BackgroundGroupType.General:
                if (SelectedGeneralBackground == null)
                    return false;

                SelectedGeneralBackground = null;
                return true;

            default:
                return false;
        }
    }


    public bool Equals(RoleBackground? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Role.Equals(other.Role)
            && Equals(SelectedBabyBackground, other.SelectedBabyBackground)
            && Equals(SelectedAdultBackground, other.SelectedAdultBackground)
            && Equals(SelectedGeneralBackground, other.SelectedGeneralBackground);
    }

    public override bool Equals(object? obj)
    {
        return obj is RoleBackground other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Role, SelectedBabyBackground, SelectedAdultBackground, SelectedGeneralBackground);
    }
}