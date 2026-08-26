using System.Linq;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Stylesheets;
using Content.Shared.Traits;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{

    /// <summary>
    /// Refreshes traits selector
    /// </summary>
    public void RefreshTraits()
    {
        TraitsList.RemoveAllChildren();

        var traits = _prototypeManager.EnumeratePrototypes<TraitPrototype>().OrderBy(t => Loc.GetString(t.Name)).ToList();
        TabContainer.SetTabTitle(3, Loc.GetString("humanoid-profile-editor-traits-tab"));

        if (traits.Count < 1)
        {
            TraitsList.AddChild(new Label
            {
                Text = Loc.GetString("humanoid-profile-editor-no-traits"),
                FontColorOverride = Color.Gray,
            });
            return;
        }

        // Setup model
        Dictionary<string, List<string>> traitGroups = new();
        List<string> defaultTraits = new();
        traitGroups.Add(TraitCategoryPrototype.Default, defaultTraits);

        foreach (var trait in traits)
        {
            if (trait.Category == null)
            {
                defaultTraits.Add(trait.ID);
                continue;
            }

            if (!_prototypeManager.HasIndex(trait.Category))
                continue;

            var group = traitGroups.GetOrNew(trait.Category);
            group.Add(trait.ID);
        }

        // Create UI view from model
        foreach (var (categoryId, categoryTraits) in traitGroups)
        {
            TraitCategoryPrototype? category = null;

            if (categoryId != TraitCategoryPrototype.Default)
            {
                category = _prototypeManager.Index<TraitCategoryPrototype>(categoryId);
                // Label
                TraitsList.AddChild(new Label
                {
                    Text = Loc.GetString(category.Name),
                    Margin = new Thickness(0, 10, 0, 0),
                    StyleClasses = { StyleClass.LabelHeading },
                });
            }

            // RAYTEN STARTS - ТРЕЙТЫ СОРТИРУЮТСЯ ТАК - СПЕРВА ПОЛОЖИТЕЛЬНЫЕ, ПОТОМ ОТРИЦАТЕЛЬНЫЕ
            var sortedCategoryTraits = categoryTraits
                .Select(id => _prototypeManager.Index<TraitPrototype>(id))
                .OrderByDescending(t => t.Cost > 0)
                .ThenByDescending(t => t.Cost)
                .ThenBy(t => t.Cost)
                .ToList();
            // RAYTEN ENDS

            List<TraitPreferenceSelector?> selectors = new();
            var selectionCount = 0;

            // Сначала вычисляем общую стоимость выбранных трейтов
            foreach (var trait in sortedCategoryTraits)
            {
                if (Profile?.TraitPreferences.Contains(trait.ID) == true)
                    selectionCount += trait.Cost;
            }

            // RAYTEN STARTS
            foreach (var trait in sortedCategoryTraits)
            // RAYTEN ENDS
            {
                var isSelected = Profile?.TraitPreferences.Contains(trait.ID) == true;
                var selector = new TraitPreferenceSelector(trait);
                selector.Preference = isSelected;

                var canSelect = true;
                if (category is { MaxTraitPoints: >= 0 } && !isSelected)
                {
                    var newTotal = selectionCount + trait.Cost;
                    canSelect = newTotal <= category.MaxTraitPoints;
                }

                selector.PreferenceChanged += preference =>
                {
                    if (preference)
                    {
                        if (category is { MaxTraitPoints: >= 0 })
                        {
                            var currentTotal = 0;
                            foreach (var t in sortedCategoryTraits)
                            {
                                if (Profile?.TraitPreferences.Contains(t.ID) == true)
                                    currentTotal += t.Cost;
                            }
                            if (currentTotal + trait.Cost > category.MaxTraitPoints)
                            {
                                RefreshTraits();
                                return;
                            }
                        }
                        Profile = Profile?.WithTraitPreference(trait.ID, _prototypeManager);
                    }
                    else
                    {
                        Profile = Profile?.WithoutTraitPreference(trait.ID, _prototypeManager);
                    }

                    SetDirty();
                    RefreshTraits();
                };

                if (!canSelect && !isSelected)
                {
                    selector.Checkbox.Disabled = true;
                    selector.Checkbox.Label.FontColorOverride = Color.Gray;
                }
                else if (category is { MaxTraitPoints: >= 0 } && isSelected)
                {
                    selector.Checkbox.Label.FontColorOverride = null;
                }

                selectors.Add(selector);
            }

            // Selection counter
            if (category is { MaxTraitPoints: >= 0 })
            {
                TraitsList.AddChild(new Label
                {
                    Text = Loc.GetString("humanoid-profile-editor-trait-count-hint", ("current", selectionCount), ("max", category.MaxTraitPoints)),
                    FontColorOverride = Color.Gray
                });
            }

            foreach (var selector in selectors)
            {
                if (selector == null)
                    continue;

                TraitsList.AddChild(selector);
            }
        }
    }
}
