using Robust.Client.Player;
using Robust.Client.GameObjects;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.GameObjects;
using Content.Shared.Rounding;
using Content.Shared.Chemistry;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;

namespace Content.Client.Vanilla.Skill
{
    public sealed class FakeChemSystem : EntitySystem
    {
        [Dependency] private readonly IPlayerManager _player = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly SharedAppearanceSystem _AppearanceSystem = default!;
        private bool _done = false;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<UpdateChemicalVisualsOnClient>(LetsTrue);
        }
        private void LetsTrue(UpdateChemicalVisualsOnClient msg, EntitySessionEventArgs args)
        {
            var playerEntity = _player.LocalPlayer?.ControlledEntity;
            if (playerEntity == null || !TryComp<SkillComponent>(playerEntity, out var skillComp))
                return;

            if ((int)skillComp.ChemistryLevel < 2)
                return;

            var query = EntityQueryEnumerator<SolutionContainerVisualsComponent, AppearanceComponent>();
            while (query.MoveNext(out var uid, out var component, out var appearance))
            {
                TrueVisualsForSolutionContainer(uid, component);
            }

        }

        private void TrueVisualsForSolutionContainer(EntityUid uid, SolutionContainerVisualsComponent component)
        {
            // Проверяем, есть ли у объекта компонент спрайта
            if (!TryComp<SpriteComponent>(uid, out var spriteComp))
                return;

            // Проверяем, есть ли данные о заполнении (fillFraction)
            if (!_AppearanceSystem.TryGetData(uid, SolutionContainerVisuals.FillFraction, out float fraction))
                return;

            if (!spriteComp.LayerMapTryGet(component.Layer, out var fillLayer))
                return;

            // Ограничиваем заполнение от 0 до 1
            fraction = MathHelper.Clamp(fraction, 0f, 1f);

            int maxFillLevels = component.MaxFillLevels;
            string? fillBaseName = component.FillBaseName;
            bool changeColor = component.ChangeColor;

            if (component.Metamorphic)
            {
                if (spriteComp.LayerMapTryGet(component.BaseLayer, out var baseLayer))
                {
                    bool hasOverlay = spriteComp.LayerMapTryGet(component.OverlayLayer, out var overlayLayer);

                    if (_AppearanceSystem.TryGetData<string>(uid, SolutionContainerVisuals.BaseOverride, out var baseOverride))
                    {
                        _prototype.TryIndex<ReagentPrototype>(baseOverride, out var reagentProto);

                        if (reagentProto?.MetamorphicSprite is { } sprite)
                        {
                            spriteComp.LayerSetSprite(baseLayer, sprite);

                            if (reagentProto.MetamorphicMaxFillLevels > 0)
                            {
                                spriteComp.LayerSetVisible(fillLayer, true);
                                maxFillLevels = reagentProto.MetamorphicMaxFillLevels;
                                fillBaseName = reagentProto.MetamorphicFillBaseName;
                                changeColor = reagentProto.MetamorphicChangeColor;
                            }
                            else
                            {
                                spriteComp.LayerSetVisible(fillLayer, false);
                            }

                            if (hasOverlay)
                                spriteComp.LayerSetVisible(overlayLayer, false);
                        }
                        else
                        {
                            spriteComp.LayerSetVisible(fillLayer, true);

                            if (hasOverlay)
                                spriteComp.LayerSetVisible(overlayLayer, true);

                            if (component.MetamorphicDefaultSprite != null)
                                spriteComp.LayerSetSprite(baseLayer, component.MetamorphicDefaultSprite);
                        }
                    }
                }
            }
            else
            {
                spriteComp.LayerSetVisible(fillLayer, true);
            }

            int fillLevel = ContentHelpers.RoundToLevels(fraction, 1, maxFillLevels+1);

            if (fillLevel > 0)
            {
                
                if (fillBaseName != null)
                {
                    var stateName = fillBaseName + fillLevel;
                    spriteComp.LayerSetState(fillLayer, stateName);
                    if (component.ChangeColor && _AppearanceSystem.TryGetData(uid, SolutionContainerVisuals.Color, out Color color))
                        spriteComp.LayerSetColor(fillLayer, color);
                    else
                        spriteComp.LayerSetColor(fillLayer, Color.White);
                }
            }
            else
            {
                if (component.EmptySpriteName != null)
                    spriteComp.LayerSetState(fillLayer, component.EmptySpriteName);
                else
                    spriteComp.LayerSetVisible(fillLayer, false);
            }
        }
    }

}
