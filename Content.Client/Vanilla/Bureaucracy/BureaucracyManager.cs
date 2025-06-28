using Content.Client.UserInterface;
using Content.Client.Hands.Systems;
using Content.Shared.Vanilla.Bureaucracy;
using Content.Shared.Verbs;
using Content.Shared.Paper;
using Robust.Client.Player;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;
using System.Linq;
using Robust.Shared.Prototypes;
using Content.Shared.Vanilla.Skill;

namespace Content.Client.Vanilla.Bureaucracy
{
    public sealed class BureaucracyManager : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly HandsSystem _handSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        }

        private void OnGetVerbs(GetVerbsEvent<Verb> args)
        {
            // Проверяем, если объект — это бумага
            if (!TryComp<PaperComponent>(args.Target, out var paper))
                return;

            if (args.Hands == null || args.Using == null || !args.CanAccess || !args.CanInteract)
                return;

            if(paper.StampedBy.Count > 0)
                return;

            if (!PlayerHasPen(args.User))
                return;

            if (!PlayerHasSkill(args.User))
                return;

            AddDocumentCategories(args);
        }

        // Обновленный метод для добавления документов в категории
        private void AddDocumentCategories(GetVerbsEvent<Verb> args)
        {
            var documents = _prototypeManager.EnumeratePrototypes<BureaucracyDocumentPrototype>().ToList();

            // Проверяем, были ли уже добавлены подкатегории в Bureaucracy
            if (!VerbCategory.Bureaucracy.SubCategories.Any())
            {
                // Добавляем подкатегории в Bureaucracy только если их нет
                if (documents.Any(doc => doc.Category == "Order"))
                    VerbCategory.Bureaucracy.AddSubCategory(VerbCategory.BureaucracyOrder);

                if (documents.Any(doc => doc.Category == "Report"))
                    VerbCategory.Bureaucracy.AddSubCategory(VerbCategory.BureaucracyReports);

                if (documents.Any(doc => doc.Category == "Request"))
                    VerbCategory.Bureaucracy.AddSubCategory(VerbCategory.BureaucracyRequest);
            }
            args.ExtraCategories.Add(VerbCategory.Bureaucracy);

            if(_prototypeManager.TryIndex<BureaucracyDocumentPrototype>("BaseBuro", out var BaseBuro))
            {
                var baseVerb = new Verb
                {
                    Text = Loc.GetString(BaseBuro.label),
                    Category = BaseBuro.GetCategory(),
                    Priority = BaseBuro.Priority,
                    Icon = null,
                    ClientExclusive = true,
                    Act = () =>
                    {
                        HandleWriteAction(args.Target, BaseBuro.ID);
                    }
                };
                args.Verbs.Add(baseVerb);
            }


            // Перебираем все документы и добавляем их в соответствующие категории
            foreach (var document in documents)
            {
                var documentSubVerb = new Verb
                {
                    Text = Loc.GetString(document.label),
                    Category = document.GetCategory(),
                    Priority = document.Priority,
                    Icon = null,
                    ClientExclusive = true,
                    Act = () =>
                    {
                        HandleWriteAction(args.Target, document.ID);
                    }
                };

                // Добавляем кнопку для документа в список
                args.Verbs.Add(documentSubVerb);
            }
        }

        // Обработка действия "Записать" на бумаге
        private void HandleWriteAction(EntityUid target, string ID)
        {
            RaiseNetworkEvent(new RequestWriteOnDockEvent(GetNetEntity(target), ID));
        }

        // Проверка наличия ручки у игрока
        private bool PlayerHasPen(EntityUid user)
        {

            if (!_handSystem.TryGetActiveItem(user, out var heldEntity))
                return false;

            if (!HasComp<BureaucracyPenComponent>(heldEntity))
                return false;

            return true;
        }
        private bool PlayerHasSkill(EntityUid user)
        {
            if (!TryComp<SkillComponent>(user, out var skillcomp))
                return false;

            return skillcomp.Bureaucracy;
        }


    }
}
