using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.IdentityManagement;
using Content.Client.Vanilla.Games.TTT.UI;
using Robust.Shared.Timing;
using Robust.Client.UserInterface;
using JetBrains.Annotations;
namespace Content.Client.Vanilla.Games.TTT;

[UsedImplicitly]
public sealed partial class TTTBodyExamineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IGameTiming _timing = default!;
    [ViewVariables]
    private TTTBodyExamineMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindowCenteredLeft<TTTBodyExamineMenu>();
        var name = Identity.Name(Owner, EntMan);
        _menu.Title = Loc.GetString("ttt-examine-body-title", ("ownerName", name));
        if (EntMan.TryGetComponent<TTTDeadBodyComponent>(Owner, out var body))
        {
            var time = _timing.CurTime - body.DeathTime;
            var minutes = (int)time.TotalMinutes;
            var seconds = time.Seconds;

            var deathTimeText = $"умер {minutes}м {seconds}с назад";
            _menu.DeathTimeLabel.SetMessage(deathTimeText);

            var gunText = EntMan.TryGetComponent<MetaDataComponent>(body.Gun, out var gunMeta)
                ? $"Его убили из {gunMeta.EntityName}"
                : "Его убили из неизвестного оружия";

            _menu.GunLabel.SetMessage(gunText);
        }

        if (EntMan.TryGetComponent<TTTMarkerComponent>(Owner, out var marker))
        {
            var roleText = $"Он был {marker.GetUIRoleName()}";
            _menu.RoleLabel.SetMessage(roleText);
        }

        _menu.SpriteView.SetEntity(Owner);
        _menu.NameLabel.SetMessage(name);
    }
}
