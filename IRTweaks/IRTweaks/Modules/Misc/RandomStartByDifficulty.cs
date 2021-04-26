﻿using BattleTech;
using BattleTech.UI;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech.UI.TMProWrapper;
using UnityEngine;
using UnityEngine.UI;

namespace IRTweaks.Modules.Misc
{



    [HarmonyPatch(typeof(LineOfSight), "FindSecondaryImpactTarget")]
    static class LineOfSight_FindSecondaryImpactTarget
    {
        static void Postfix(LineOfSight __instance, RaycastHit[] rayInfos, AbstractActor attacker,
            ICombatant initialTarget, Vector3 attackPosition, ref Vector3 impactPoint, ref bool __result)
        {
            var combat = UnityGameInstance.BattleTechGame.Combat;
            if (!combat.Constants.ToHit.StrayShotsEnabled)
            {
                var num = float.MaxValue;
                for (int i = 0; i < rayInfos.Length; i++)
                {
                    if (rayInfos[i].distance < num)
                    {
                        impactPoint = rayInfos[i].point;
                        num = rayInfos[i].distance;
                    }
                }
                __result = true;
            }
        }
    }


    [HarmonyPatch(typeof(SimGameDifficultySettingsModule), "InitSettings")]
    static class SimGameDifficultySettingsModule_InitSettings_Patch
    {
        private static MethodInfo _getItemMethodUInfo = AccessTools.Method(typeof(SimGameDifficultySettingsModule), "GetItem");

        static void Prefix(SimGameDifficultySettingsModule __instance)
        {
            var startonly = GameObject.Find("OBJ_startOnly_settings");
            var transformLayoutGroup = startonly.GetComponent<RectTransform>().GetComponent<GridLayoutGroup>();
            transformLayoutGroup.cellSize = new Vector2(375, 40);
            transformLayoutGroup.spacing = new Vector2(25, 22);
        }
        static void Postfix(SimGameDifficultySettingsModule __instance, SimGameDifficulty ___cachedDiff, string ___ironManModeId, string ___autoEquipMechsId, string ___mechPartsReqId, string ___skipPrologueId, string ___randomMechId, string ___argoUpgradeCostId, SGDSToggle ___ironManModeToggle, SGDSDropdown ___mechPartsReqDropdown, GameObject ___disabledOverlay, List<SGDSDropdown> ___activeDropdowns, List<SGDSToggle> ___activeToggles, List<SGDSDropdown> ___cachedDropdowns, List<SGDSToggle> ___cachedToggles, SGDSToggle ___togglePrefab, SGDSDropdown ___dropdownPrefab)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var existingStartOnlyVars = new List<string>()
            {
                ___ironManModeId,
                ___autoEquipMechsId,
                ___mechPartsReqId,
                ___skipPrologueId,
                ___randomMechId,
                ___argoUpgradeCostId
            };

            ___cachedDiff = UnityGameInstance.BattleTechGame.DifficultySettings;
            var settings = ___cachedDiff.GetSettings();
            settings.Sort(delegate(SimGameDifficulty.DifficultySetting a, SimGameDifficulty.DifficultySetting b)
            {
                if (a.UIOrder != b.UIOrder)
                {
                    return a.UIOrder.CompareTo(b.UIOrder);
                }
                return a.Name.CompareTo(b.Name);
            });

            foreach (var setting in settings)
            {
                if (setting.Visible)
                {
                    int curSettingIndex = ___cachedDiff.GetCurSettingIndex(setting.ID);

                    if (setting.StartOnly && existingStartOnlyVars.All(x => x != setting.ID))
                    {
                        if (!setting.Toggle)
                        {
                            var sourceSettingDropDownGO = ___mechPartsReqDropdown.gameObject;

                            GameObject newDropDownObject = UnityEngine.Object.Instantiate<GameObject>(sourceSettingDropDownGO, sourceSettingDropDownGO.transform.parent);

                            SGDSDropdown newDropDown = newDropDownObject.GetComponentInParent<SGDSDropdown>();

                            var dropdown = Traverse.Create(newDropDown).Field("dropdown").GetValue<HBS_Dropdown>();
                            var dropdownrect = dropdown.gameObject.GetComponent<RectTransform>();
                            dropdownrect.sizeDelta = new Vector2(170, 40);

                            var dropdownLabel = Traverse.Create(dropdown).Field("m_CaptionText")
                                .GetValue<LocalizableText>();
                            dropdownLabel.enableWordWrapping = false;

                            if (!ModState.InstantiatedDifficultySettings.instantiatedDropdowns.Contains(newDropDown))
                            {
                                ___activeDropdowns.Add(newDropDown);
                                newDropDown.Initialize(__instance, setting, curSettingIndex);
                                newDropDown.gameObject.SetActive(true);
                                ModState.InstantiatedDifficultySettings.instantiatedDropdowns.Add(newDropDown);
                            }
                        }
                        else if (setting.Toggle)
                        {
                            var sourceDiffToggleGO = ___ironManModeToggle.gameObject;
                            GameObject sourceDiffToggle = UnityEngine.Object.Instantiate<GameObject>(sourceDiffToggleGO, sourceDiffToggleGO.transform.parent);
                            SGDSToggle newToggle = sourceDiffToggle.GetComponentInParent<SGDSToggle>();

                            if (!ModState.InstantiatedDifficultySettings.instantiatedToggles.Contains(newToggle))
                            {
                                ___activeToggles.Add(newToggle);
                                newToggle.Initialize(__instance, setting, curSettingIndex);
                                newToggle.gameObject.SetActive(true);
                                ModState.InstantiatedDifficultySettings.instantiatedToggles.Add(newToggle);
                            }
                        }
                    }
                }
            }

            var newDisabledOverlay =
                UnityEngine.Object.Instantiate<GameObject>(___disabledOverlay, ___disabledOverlay.transform.parent);
           ___disabledOverlay.SetActive(false);
           newDisabledOverlay.SetActive(!__instance.CanModifyStartSettings);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "AddRandomStartingMechs")]
    static class RandomStartByDifficulty_SimGameState_AddRandomStartingMechs
    {
        static bool Prepare() => Mod.Config.Fixes.RandomStartByDifficulty;

        static void Prefix(SimGameState __instance)
        {
            Mod.Log.Trace?.Write("SGS:ARSM entered.");
            SimGameConstantOverride sgco = __instance.ConstantOverrides;

            if (sgco.ConstantOverrides.ContainsKey("CareerMode"))
            {
                // Patch starting mechs
                if (sgco.ConstantOverrides["CareerMode"].ContainsKey(ModStats.HBS_RandomMechs))
                {
                    string startingMechsS = sgco.ConstantOverrides["CareerMode"][ModStats.HBS_RandomMechs];
                    Mod.Log.Info?.Write($"Replacing starting random mechs with:{startingMechsS}");
                    string[] startingMechs = startingMechsS.Split(',');
                    __instance.Constants.CareerMode.StartingRandomMechLists = startingMechs;
                }
                else
                {
                    Mod.Log.Debug?.Write($"key: {ModStats.HBS_RandomMechs} not found");
                }

                // Patch faction reputation
                if (sgco.ConstantOverrides["CareerMode"].ContainsKey(ModStats.HBS_FactionRep))
                {
                    string factionRepS = sgco.ConstantOverrides["CareerMode"][ModStats.HBS_FactionRep];
                    string[] factions = factionRepS.Split(',');
                    foreach (string factionToken in factions)
                    {
                        string[] factionSplit = factionToken.Split(':');
                        string factionId = factionSplit[0];
                        int factionRep = int.Parse(factionSplit[1]);
                        Mod.Log.Info?.Write($"Applying rep: {factionRep} to faction: ({factionId})");
                        FactionDef factionDef = FactionDef.GetFactionDefByEnum(__instance.DataManager, factionId);
                        __instance.AddReputation(factionDef.FactionValue, factionRep, false);
                    }
                }
                else
                {
                    Mod.Log.Debug?.Write($"key: {ModStats.HBS_RandomMechs} not found");
                }

            }
            else if (!sgco.ConstantOverrides.ContainsKey("CareerMode"))
            {
                Mod.Log.Debug?.Write("key:CareerMode not found");
            }
        }
    }

    [HarmonyPatch(typeof(SimGameConstantOverride), "ApplyOverride")]
    static class RandomStartByDifficulty_SimGameConstantOverride_ApplyOverride
    {
        static bool Prepare() => Mod.Config.Fixes.RandomStartByDifficulty;

        static void Postfix(SimGameConstantOverride __instance, string constantType, string constantName, SimGameState ___simState)
        {
            Mod.Log.Trace?.Write("SGCO:AO entered.");

            if (constantName != null && constantName.ToLower().Equals(ModStats.HBS_StrayShotEnabler.ToLower()))
            {
                bool value = Convert.ToBoolean(__instance.ConstantOverrides[constantType][constantName]);
                Mod.Log.Debug?.Write($" Setting StrayShotsEnabled to {value} ");
                ToHitConstantsDef thcd = ___simState.CombatConstants.ToHit;
                thcd.StrayShotsEnabled = value;

                Traverse traverse = Traverse.Create(___simState.CombatConstants).Property("ToHit");
                traverse.SetValue(thcd);
                Mod.Log.Debug?.Write($" Replaced ToHit");
            }

            if (constantName != null && constantName.ToLower().Equals(ModStats.HBS_StrayShotHitsUnits.ToLower()))
            {
                bool value = Convert.ToBoolean(__instance.ConstantOverrides[constantType][constantName]);
                Mod.Log.Debug?.Write($" Setting StrayShotsHitUnits to {value} ");
                ToHitConstantsDef thcd = ___simState.CombatConstants.ToHit;
                thcd.StrayShotsHitUnits = value;

                Traverse traverse = Traverse.Create(___simState.CombatConstants).Property("ToHit");
                traverse.SetValue(thcd);
                Mod.Log.Debug?.Write($" Replaced ToHit");
            }

            if (constantName != null && constantName.ToLower().Equals(ModStats.HBS_StrayShotValidTargets.ToLower()))
            {
                string value = __instance.ConstantOverrides[constantType][constantName];
                Mod.Log.Debug?.Write($" Setting StrayShotValidTargets to {value} ");
                ToHitConstantsDef thcd = ___simState.CombatConstants.ToHit;
                thcd.StrayShotValidTargets = (StrayShotValidTargets)Enum.Parse(typeof(StrayShotValidTargets), value);

                Traverse traverse = Traverse.Create(___simState.CombatConstants).Property("ToHit");
                traverse.SetValue(thcd);
                Mod.Log.Debug?.Write($" Replaced ToHit");
            }
        }
    }

    [HarmonyPatch(typeof(SGDifficultySettingObject), "CurCareerModifier")]
    static class RandomStartByDifficulty_SGDifficultySettingObject_CurCareerModifier
    {
        static bool Prepare() => Mod.Config.Fixes.RandomStartByDifficulty;

        static bool Prefix(SGDifficultySettingObject __instance, ref float __result, int ___curIdx)
        {
            Mod.Log.Trace?.Write("SGDSO:CCM entered.");

            float careerScoreModifier = 0f;
            if (__instance != null && __instance.Setting != null && __instance.Setting.Options != null && __instance.Setting.Options.Count > ___curIdx)
            {
                careerScoreModifier = __instance.Setting.Options[___curIdx].CareerScoreModifier;
            }

            __result = (careerScoreModifier <= -1f) ? 0f : careerScoreModifier;

            return false;
        }

    }

}
