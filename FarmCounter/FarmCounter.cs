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
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

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

    internal static readonly Harmony harmony = new Harmony(PluginName);

    private void Awake() {
      try {
        harmony.PatchAll();
      } catch (Exception ex) {
        Logger.LogError($"Exception installing patches for {PluginName}: {ex}");
      }
    }
  }
}
