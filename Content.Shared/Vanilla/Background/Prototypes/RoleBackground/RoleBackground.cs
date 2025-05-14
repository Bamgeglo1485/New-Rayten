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
using Content.Shared.Vanilla.Skill;

namespace Content.Shared.Vanilla.Background;


[Serializable, NetSerializable, DataDefinition]
public sealed partial class RoleBackground : IEquatable<RoleBackground>
{
    [DataField]
    public ProtoId<RoleBackgroundPrototype> Role;

    [DataField]
    public ProtoId<BackgroundPrototype>? SelectedBabyBackground;
    [DataField]
    public ProtoId<BackgroundPrototype>? SelectedAdultBackground;
    [DataField]
    public ProtoId<BackgroundPrototype>? SelectedGeneralBackground;
    public int SkillpointCredit = 0;
    [DataField]
    public Dictionary<skillType, SkillLevel> AddedBasicSkills = new();
    [DataField]
    public HashSet<skillType> AddedEasySkills = new();

    public RoleBackground(ProtoId<RoleBackgroundPrototype> role)
    {
        Role = role;
    }

    public RoleBackground Clone()
    {
        return new RoleBackground(Role)
        {
            SkillpointCredit = SkillpointCredit,
            SelectedBabyBackground = SelectedBabyBackground,
            SelectedAdultBackground = SelectedAdultBackground,
            SelectedGeneralBackground = SelectedGeneralBackground,
            AddedBasicSkills = new Dictionary<skillType, SkillLevel>(AddedBasicSkills),
            AddedEasySkills = new HashSet<skillType>(AddedEasySkills),
        };
    }


    /// <summary>
    /// Ensures all prototypes exist and effects can be applied.
    /// </summary>
    public void EnsureValid(HumanoidCharacterProfile profile, ICommonSession session, IDependencyCollection collection)
    {
        var protoManager = collection.Resolve<IPrototypeManager>();
        // 1. Проверка, что роль существует
        if (!protoManager.TryIndex(Role, out RoleBackgroundPrototype? roleProto))
        {
            SelectedBabyBackground = null;
            SelectedAdultBackground = null;
            SelectedGeneralBackground = null;
            return;
        }
        // 2. Проверка предысторий по типу
        void ValidateBackground(ref ProtoId<BackgroundPrototype>? selectedBackgroundId, string? groupId, BackgroundGroupType requiredType)
        {
            if (selectedBackgroundId == null || string.IsNullOrEmpty(groupId))
            {
                selectedBackgroundId = null;
                return;
            }

            // Проверка, что группа существует
            if (!protoManager.TryIndex<BackgroundGroupPrototype>(groupId, out var groupProto))
            {
                selectedBackgroundId = null;
                return;
            }

            // Проверка, что группа соответствует типу
            if (groupProto.Type != requiredType)
            {
                selectedBackgroundId = null;
                return;
            }

            // Проверка, что предыстория существует
            if (!protoManager.TryIndex<BackgroundPrototype>(selectedBackgroundId.Value, out var backgroundProto))
            {
                selectedBackgroundId = null;
                return;
            }

            // Проверка, что предыстория входит в группу
            if (!groupProto.Backgrounds.Contains(selectedBackgroundId.Value))
            {
                selectedBackgroundId = null;
            }
        }
        ValidateBackground(ref SelectedBabyBackground, roleProto.Baby, BackgroundGroupType.Baby);
        ValidateBackground(ref SelectedAdultBackground, roleProto.Adult, BackgroundGroupType.Adult);
        ValidateBackground(ref SelectedGeneralBackground, roleProto.General, BackgroundGroupType.General);
        // 3. Проверка что выбрана либо одна основная предыстория либо детская + взрослая
        // 4. Проверка на навыки

    }

    public void SetDefault(HumanoidCharacterProfile? profile)
    {
        if (profile == null)
            return;
        SelectedBabyBackground = null;
        SelectedAdultBackground = null;
        SelectedGeneralBackground = null;
        SkillpointCredit = 0;
        AddedBasicSkills = new();
        AddedEasySkills = new();
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


    public bool Equals(RoleBackground? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Role.Equals(other.Role)
            && Equals(SelectedBabyBackground, other.SelectedBabyBackground)
            && Equals(SelectedAdultBackground, other.SelectedAdultBackground)
            && Equals(SelectedGeneralBackground, other.SelectedGeneralBackground)
            && SkillpointCredit == other.SkillpointCredit
            && AddedBasicSkills.Count == other.AddedBasicSkills.Count
            && AddedBasicSkills.All(kv => other.AddedBasicSkills.TryGetValue(kv.Key, out var val) && val == kv.Value)
            && AddedEasySkills.SetEquals(other.AddedEasySkills);
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