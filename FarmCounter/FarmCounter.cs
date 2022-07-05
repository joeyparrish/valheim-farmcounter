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
    public const string PluginGUID = "io.github.joeyparrish";
    public const string PluginName = "FarmCounter";
    public const string PluginVersion = ModVersion.String;

    private static readonly Harmony harmony = new Harmony(PluginName);

    private void Awake() {
      try {
        harmony.PatchAll();
      } catch (Exception ex) {
        Logger.LogError($"Exception installing patches for {PluginName}: {ex}");
      }
    }

    private const string workbenchName = "$piece_workbench";
    private static float workbenchRange = 20f;  // standard workbench range

    // If there's a mod installed that modifies the workbench range, sniff out
    // the range at runtime.  This is compatible with Valheim Plus.
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Start))]
    class SniffWorkbenchRange_Patch {
      static void Postfix(CraftingStation __instance) {
        if (__instance.m_name == workbenchName) {
          workbenchRange = __instance.m_rangeBuild;
        }
      }
    }

    // Workbenches in range of a sign define the bounds of the farm.
    private static List<CraftingStation> FindWorkbenches(Vector3 position) {
      var workbenches = new List<CraftingStation>();
      CraftingStation.FindStationsInRange(
          workbenchName, position, workbenchRange, workbenches);
      return workbenches;
    }

    // Return true if the character is inside the bounds of the farm.
    private static bool IsInsideFarm(
        Character character, List<CraftingStation> workbenches) {
      foreach (var workbench in workbenches) {
        var distance = Vector3.Distance(
            character.transform.position, workbench.transform.position);
        if (distance < workbenchRange) {
          return true;
        }
      }
      return false;
    }

    private static string ComputeDisplayedSignText(Sign sign) {
      var signText = sign.GetText();
      if (!signText.Contains("$wild_") && !signText.Contains("$tame_") &&
          !signText.Contains("$all_") && !signText.Contains("$workbenches")) {
        return signText;
      }

      var workbenches = FindWorkbenches(sign.transform.position);

      var countWild = new Dictionary<string, int>();
      var countTamed = new Dictionary<string, int>();
      var countAll = new Dictionary<string, int>();

      foreach (var character in Character.GetAllCharacters()) {
        var baseName = character.m_name.Replace("$enemy_", "");
        if (!countTamed.ContainsKey(baseName)) {
          countWild[baseName] = 0;
          countTamed[baseName] = 0;
          countAll[baseName] = 0;
        }

        if (IsInsideFarm(character, workbenches)) {
          countAll[baseName] += 1;
          if (character.IsTamed()) {
            countTamed[baseName] += 1;
          } else {
            countWild[baseName] += 1;
          }
        }
      }

      // Sort the keys, longest first, since some monster names are substrings
      // of others.  By replacing the longest keys first, we make sure we do
      // the right thing with $num_boar and $num_boarpiggy.
      var keys = new List<string>(countTamed.Keys);
      keys.Sort((x, y) => y.Length.CompareTo(x.Length));

      foreach (var key in keys) {
        signText = signText.Replace($"$wild_{key}", countWild[key].ToString());
        signText = signText.Replace($"$tame_{key}", countTamed[key].ToString());
        signText = signText.Replace($"$all_{key}", countAll[key].ToString());
      }
      signText = signText.Replace("$workbenches", workbenches.Count.ToString());
      return signText;
    }

    // Does not change the actual text stored, only the text displayed.
    // The original, unaltered text will show up for the editor.
    [HarmonyPatch(typeof(Sign), nameof(Sign.UpdateText))]
    class ReplaceDisplayedSignText_Patch {
      static IEnumerable<CodeInstruction> Transpiler(
          IEnumerable<CodeInstruction> instructions,
          ILGenerator generator) {
        var replacementTextMethod = typeof(FarmCounterMod).GetMethod(
            nameof(FarmCounterMod.ComputeDisplayedSignText),
            BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var code in instructions) {
          if (code.opcode == OpCodes.Call &&
              (code.operand as MethodInfo).Name == "GetText") {
            yield return new CodeInstruction(
                OpCodes.Call, replacementTextMethod);
          } else {
            yield return code;
          }
        }
      }
    }
  }
}
