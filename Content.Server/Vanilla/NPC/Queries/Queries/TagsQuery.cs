using Robust.Shared.Prototypes;
using Content.Server.NPC.Queries.Queries;
using Content.Shared.Tag;
namespace Content.Server.Vanilla.NPC.Queries.Queries;

public sealed partial class TagsQuery : UtilityQuery
{
    [DataField("tags", required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = new();
}
