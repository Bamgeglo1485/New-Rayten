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

namespace Content.Client.Vanilla.Skill;
public sealed class FakeChemSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedAppearanceSystem _AppearanceSystem = default!;
    private EntityUid _globPlayerEntity;
    private SkillLevel _lastlvl;

    public override void Initialize()
    {
        base.Initialize();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var playerEntity = _player.LocalPlayer?.ControlledEntity;

        if(playerEntity == null)
            return;

        if(_globPlayerEntity == null )
            _globPlayerEntity = playerEntity.Value;

        if(_globPlayerEntity == playerEntity)
        {
            if(!TryComp<SkillComponent>(playerEntity, out var skillComp))
                return;

            if(skillComp.ChemistryLevel == _lastlvl)
                return;

            updateallchem(skillComp);
            _globPlayerEntity = playerEntity.Value;
            _lastlvl = skillComp.ChemistryLevel;
        }
        else
        {
            TryComp<SkillComponent>(playerEntity, out var skillComp);

            updateallchem(skillComp);
            _globPlayerEntity = playerEntity.Value;
        }
    }

    private void updateallchem(SkillComponent? skillComp)
    {
        var query = EntityQueryEnumerator<SolutionContainerVisualsComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var component, out var appearance))
        {
            UpdateVisualsForSolutionContainer(uid, component, skillComp);
        }  
    }

    private void UpdateVisualsForSolutionContainer(EntityUid uid, SolutionContainerVisualsComponent component, SkillComponent? skillComp)
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
        var fillSprite = component.MetamorphicDefaultSprite;

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
                            fillSprite = sprite;
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
                if (fillSprite != null)
                    spriteComp.LayerSetSprite(fillLayer, fillSprite);
                spriteComp.LayerSetState(fillLayer, stateName);

                if (changeColor && _AppearanceSystem.TryGetData<Color>(uid, SolutionContainerVisuals.Color, out var color))
                {
                    if (_AppearanceSystem.TryGetData<string>(uid, SolutionContainerVisuals.BaseOverride, out var baseOverride))
                    {
                        _prototype.TryIndex<ReagentPrototype>(baseOverride, out var reagentProto);
                        if (reagentProto?.Recognizable == false)
                            ApplyFakeChemColor(uid, ref color, skillComp);
                    }
                    else
                        ApplyFakeChemColor(uid, ref color, skillComp);

                    spriteComp.LayerSetColor(fillLayer, color);
                }
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

    private void ApplyFakeChemColor(EntityUid uid, ref Color color, SkillComponent? skillComp)
    {
        if (TryComp<FakeChemComponent>(uid, out var fakeChem))
        {
            if(skillComp ==null)
            {
                color = fakeChem.FakeColor;
            }
            else if (skillComp.ChemistryLevel == SkillLevel.None || skillComp.ChemistryLevel == SkillLevel.Basic)
            {
                color = fakeChem.FakeColor;
            }
        }
    }

}


