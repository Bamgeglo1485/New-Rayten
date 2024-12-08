using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Skill
{
    [Serializable, NetSerializable]
    public sealed class UpdateCharacterSkillsRequestEvent : EntityEventArgs
    {
        public readonly NetEntity NetEntity;

        public UpdateCharacterSkillsRequestEvent(NetEntity netEntity)
        {
            NetEntity = netEntity;
        }
    }
}
