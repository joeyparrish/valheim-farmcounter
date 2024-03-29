﻿/**
 * FarmCounter - A Valheim Mod
 * Copyright (C) 2022 Joey Parrish
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[assembly: AssemblyTitle("FarmCounter")]
[assembly: AssemblyProduct("FarmCounter")]
[assembly: AssemblyCopyright("Copyright © 2022 Joey Parrish")]
[assembly: AssemblyVersion(FarmCounter.ModVersion.String + ".0")]

namespace FarmCounter {
#if DEBUG
  // This is generated at build time for releases.
  public static class ModVersion {
    public const string String = "0.0.1";
  }
#endif

  [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
  public class FarmCounterMod : BaseUnityPlugin {
    // BepInEx' plugin metadata
    public const string PluginGUID = "io.github.joeyparrish.FarmCounter";
    public const string PluginName = "FarmCounter";
    public const string PluginVersion = ModVersion.String;

    private static readonly Harmony harmony = new Harmony(PluginName);

    private static new BepInEx.Logging.ManualLogSource Logger;

    private void Awake() {
      Logger = BepInEx.Logging.Logger.CreateLogSource("FarmCounter");

      try {
        harmony.PatchAll();
      } catch (Exception ex) {
        Logger.LogError($"Exception installing patches for {PluginName}: {ex}");
      }
    }

    public class FarmCounterBehaviour : MonoBehaviour {
      private const string workbenchName = "$piece_workbench";
      private static float workbenchRange = 20f;  // standard workbench range

      private static List<FarmCounterBehaviour> instances =
          new List<FarmCounterBehaviour>();

      private Sign sign;
      // Sorted by length, descending.
      private List<string> identifiers = new List<string>();
      private List<CraftingStation> workbenches = new List<CraftingStation>();

      private void Awake() {
        instances.Add(this);

        sign = GetComponent<Sign>();
        if (sign != null) {
          PreparseSignText();
          FindWorkbenches();
        }
      }

      private void OnDestroy() {
        instances.Remove(this);
      }

      private void FindWorkbenches() {
        // Workbenches in range of a sign define the bounds of the farm.
        // TODO: Test V+ range changes
        workbenches.Clear();
        CraftingStation.FindStationsInRange(
            workbenchName, transform.position, workbenchRange, workbenches);
      }

      private static void RecomputeAllWorkbenchesInRange() {
        foreach (var farmCounter in instances) {
          farmCounter.FindWorkbenches();
        }
      }

      private bool IsSimpleIdentifierCharacter(char ch) {
        return (ch >= 'a' && ch <= 'z') ||
               (ch >= 'A' && ch <= 'Z') ||
               (ch >= '0' && ch <= '9') ||
               (ch == '_');
      }

      private void ConsumeIdentifier(HashSet<string> identifierSet,
                                     string identifier) {
        Logger.LogDebug($"PreparseSignText: identifier \"{identifier}\"");
        if (identifier.StartsWith("all_")) {
          identifierSet.Add(identifier.Replace("all_", ""));
        } else if (identifier.StartsWith("tame_")) {
          identifierSet.Add(identifier.Replace("tame_", ""));
        } else if (identifier.StartsWith("wild_")) {
          identifierSet.Add(identifier.Replace("wild_", ""));
        }
      }

      public void PreparseSignText() {
        var signText = sign.GetText();
        var identifierSet = new HashSet<string>();
        Logger.LogDebug($"PreparseSignText: \"{signText}\"");

        bool in_identifier = false;
        bool in_simple_identifier = false;
        bool in_extended_identifier = false;
        string identifier = "";

        // Iterate to the character past the end, on purpose.  This will allow
        // us to parse strings that end with an identifier by having a virtual
        // "character" to end the string and let us run the logic to close an
        // identifier.
        for (int index = 0; index <= signText.Length; index++) {
          var ch = index < signText.Length ? signText[index] : '\0';
          if (in_identifier == false) {
            if (ch == '$') {
              in_identifier = true;
              in_simple_identifier = false;
              in_extended_identifier = false;
              identifier = "";
            }
          } else if (in_simple_identifier) {
            if (IsSimpleIdentifierCharacter(ch)) {
              identifier += ch;
            } else {
              ConsumeIdentifier(identifierSet, identifier);
              in_identifier = false;
            }
          } else if (in_extended_identifier) {
            if (ch == '}') {
              ConsumeIdentifier(identifierSet, identifier);
              in_identifier = false;
            } else {
              identifier += ch;
            }
          } else {
            // We don't know what kind of identifier this is yet.
            if (ch == '{') {
              in_extended_identifier = true;
            } else if (IsSimpleIdentifierCharacter(ch)) {
              in_simple_identifier = true;
              identifier += ch;
            }
          }
        }

        // Sort the identifiers, longest first, since some monster names are
        // substrings of others.  Later, by replacing the longest keys first, we
        // make sure we do the right thing with $num_boar and $num_boarpiggy.
        identifiers = new List<string>(identifierSet);
        identifiers.Sort((x, y) => y.Length.CompareTo(x.Length));
      }  // public void PreparseSignText

      // Return true if the character is inside the bounds of the farm.
      private bool IsInsideFarm(Character character) {
        foreach (var workbench in workbenches) {
          var distance = Vector3.Distance(
              character.transform.position, workbench.transform.position);
          if (distance < workbenchRange) {
            return true;
          }
        }
        return false;
      }

      // Called by a transpiler patch to replace the raw text in UpdateText().
      public static string ComputeDisplayedText(Sign sign) {
        var farmCounter = sign.GetComponent<FarmCounterBehaviour>();
        if (farmCounter != null) {
          return farmCounter.ComputeDisplayedText();
        }
        return sign.GetText();
      }

      private string ComputeDisplayedText() {
        var signText = sign.GetText();
        if (identifiers.Count == 0) {
          // Nothing special in this sign.
          return signText;
        }

        var countWild = new Dictionary<string, int>();
        var countTame = new Dictionary<string, int>();
        var countAll = new Dictionary<string, int>();

        foreach (var identifier in identifiers) {
          countAll[identifier] = 0;
          countWild[identifier] = 0;
          countTame[identifier] = 0;
        }

        foreach (var character in Character.GetAllCharacters()) {
          var rawName = character.m_name;
          var baseName = character.m_name.Replace("$enemy_", "");
          string nameUsed = null;

          if (identifiers.Contains(baseName)) {
            nameUsed = baseName;
          } else if (identifiers.Contains(rawName)) {
            nameUsed = rawName;
          }

          if (nameUsed != null && IsInsideFarm(character)) {
            if (character.IsTamed()) {
              countTame[nameUsed] += 1;
            } else {
              countWild[nameUsed] += 1;
            }
            countAll[nameUsed] += 1;
          }
        }

        foreach (var key in identifiers) {
          signText = signText.Replace($"$wild_{key}", countWild[key].ToString());
          signText = signText.Replace($"${{wild_{key}}}", countWild[key].ToString());
          signText = signText.Replace($"$tame_{key}", countTame[key].ToString());
          signText = signText.Replace($"${{tame_{key}}}", countTame[key].ToString());
          signText = signText.Replace($"$all_{key}", countAll[key].ToString());
          signText = signText.Replace($"${{all_{key}}}", countAll[key].ToString());
        }

        // For debugging purposes, show the number of workbenches in range.
        signText = signText.Replace(
            "$all_workbench", workbenches.Count.ToString());

        return signText;
      }

      [HarmonyPatch]
      class Patches {
        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Start))]
        [HarmonyPostfix]
        static void SniffWorkbenchRange(CraftingStation __instance) {
          if (__instance.m_name == workbenchName) {
            // If there's a mod installed that modifies the workbench range,
            // sniff out the range at runtime.  This is compatible with Valheim
            // Plus.
            if (workbenchRange != __instance.m_rangeBuild) {
              workbenchRange = __instance.m_rangeBuild;
              Logger.LogInfo($"Workbench range detected: {workbenchRange}");
            }

            // If a new workbench is created, or a workbench is started after a
            // Sign, we need to have all Signs recompute their list of
            // workbenches.
            RecomputeAllWorkbenchesInRange();
          }
        }

        // Attach the new behaviour to all Signs.
        [HarmonyPatch(typeof(Sign), nameof(Sign.Awake))]
        [HarmonyPostfix]
        static void AttachNewSignBehaviour(Sign __instance) {
          if (__instance.GetComponent<FarmCounterBehaviour>() == null) {
            __instance.gameObject.AddComponent<FarmCounterBehaviour>();
          }
        }

        // If a player changes a Sign's text, reparse it for special identifiers.
        [HarmonyPatch(typeof(Sign), nameof(Sign.SetText))]
        [HarmonyPostfix]
        static void ParseNewSignText(Sign __instance) {
          var farmCounter = __instance.GetComponent<FarmCounterBehaviour>();
          if (farmCounter != null) {
            farmCounter.PreparseSignText();
          }
        }

        // At least since January 2023, Sign.GetText() returns a value cached
        // by UpdateText().  This broke a core assumption that GetText()
        // returns the raw data and not the displayed data.  To fix this, we
        // patch over GetText() and give it the original implementation.  This
        // not only fixes much of the logic above, but ensures that the editor
        // always shows the raw text when you go to modify a sign.
        [HarmonyPatch(typeof(Sign), nameof(Sign.GetText))]
        [HarmonyPrefix]
        static bool GetRawText(Sign __instance, ref string __result) {
          __result = __instance.m_nview.GetZDO().GetString(
              "text", __instance.m_defaultText);
          // Suppress the built-in version in favor of this patch.
          return false;
        }

        // Does not change the actual text stored, only the text displayed.
        // The original, unaltered text will show up for the editor.
        [HarmonyPatch(typeof(Sign), nameof(Sign.UpdateText))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ReplaceDisplayedText(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator) {
          var replacementTextMethod = typeof(FarmCounterBehaviour).GetMethod(
              nameof(FarmCounterBehaviour.ComputeDisplayedText),
              BindingFlags.Static | BindingFlags.Public);
          var firstGetStringFound = false;

          /*
          The beginning of the original method, as of Jan 2023, looks like:

          IL_0000: newobj instance void Sign/'<>c__DisplayClass4_0'::.ctor()
          IL_0005: stloc.0
          IL_0006: ldloc.0

          IL_0007: ldarg.0
          IL_0008: stfld class Sign Sign/'<>c__DisplayClass4_0'::'<>4__this'
          IL_000d: ldloc.0

          IL_000e: ldarg.0
          IL_000f: ldfld class ZNetView Sign::m_nview
          IL_0014: callvirt instance class ZDO ZNetView::GetZDO()
          IL_0019: ldstr "text"
          IL_001e: ldarg.0
          IL_001f: ldfld string Sign::m_defaultText
          IL_0024: callvirt instance string ZDO::GetString(string, string)

          IL_0029: stfld string Sign/'<>c__DisplayClass4_0'::text

          This corresponds to this in C#:

          string text = m_nview.GetZDO().GetString("text", m_defaultText);
          */

          foreach (var code in instructions) {
            yield return code;

            // After the first call to GetString, trash the result and call our
            // ComputeDisplayedText method to replace it.
            if (!firstGetStringFound &&
                code.opcode == OpCodes.Callvirt &&
                (code.operand as MethodInfo).Name == "GetString") {
              firstGetStringFound = true;
              yield return new CodeInstruction(OpCodes.Pop);
              yield return new CodeInstruction(OpCodes.Ldarg_0);
              yield return new CodeInstruction(
                  OpCodes.Call, replacementTextMethod);
            }
          }

          if (!firstGetStringFound) {
            Logger.LogError($"Failed to patch Sign.UpdateText!");
          } else {
            Logger.LogInfo($"Successfully patched Sign.UpdateText!");
          }
        }
      }  // class Patches
    }  // class FarmCounterBehaviour
  }  // class FarmCounterMod
}  // namespace FarmCounter
