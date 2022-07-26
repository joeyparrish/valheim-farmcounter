/**
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

      public void PreparseSignText() {
        var signText = sign.GetText();
        var identifierSet = new HashSet<string>();
        Logger.LogDebug($"PreparseSignText: \"{signText}\"");

        bool in_identifier = false;
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
              identifier = "";
            }
          } else {
            if ((ch >= 'a' && ch <= 'z') ||
                (ch >= 'A' && ch <= 'Z') ||
                (ch >= '0' && ch <= '9') ||
                (ch == '_')) {
              identifier += ch;
            } else {
              Logger.LogDebug($"PreparseSignText: identifier \"{identifier}\"");
              if (identifier.StartsWith("all_")) {
                identifierSet.Add(identifier.Replace("all_", ""));
              } else if (identifier.StartsWith("tame_")) {
                identifierSet.Add(identifier.Replace("tame_", ""));
              } else if (identifier.StartsWith("wild_")) {
                identifierSet.Add(identifier.Replace("wild_", ""));
              }
              in_identifier = false;
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

      // Called by a transpiler patch instead of sign.GetText().
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
          var baseName = character.m_name.Replace("$enemy_", "");
          if (identifiers.Contains(baseName) && IsInsideFarm(character)) {
            if (character.IsTamed()) {
              countTame[baseName] += 1;
            } else {
              countWild[baseName] += 1;
            }
            countAll[baseName] += 1;
          }
        }

        foreach (var key in identifiers) {
          signText = signText.Replace($"$wild_{key}", countWild[key].ToString());
          signText = signText.Replace($"$tame_{key}", countTame[key].ToString());
          signText = signText.Replace($"$all_{key}", countAll[key].ToString());
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

          foreach (var code in instructions) {
            // Call our ComputeDisplayedText method instead of GetText.
            if (code.opcode == OpCodes.Call &&
                (code.operand as MethodInfo).Name == "GetText") {
              yield return new CodeInstruction(
                  OpCodes.Call, replacementTextMethod);
            } else {
              yield return code;
            }
          }
        }
      }  // class Patches
    }  // class FarmCounterBehaviour
  }  // class FarmCounterMod
}  // namespace FarmCounter
