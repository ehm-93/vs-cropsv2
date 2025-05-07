using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BlockEntityFarmlandV2 : BlockEntityFarmland
{
    private int mulchLevel = 0;

    public int MulchLevel {
        get => mulchLevel;
        set => mulchLevel = Math.Clamp(value, 0, 3);
    }

    [HarmonyPatchCategory("cropsv2")]
    [HarmonyPatch(typeof(BlockEntityFarmland), "updateMoistureLevel", new Type[] {
        typeof(double), typeof(float), typeof(bool), typeof(ClimateCondition)
    })]
    internal static class UpdateMoistureLevelPatch
    {
        [HarmonyPrefix]
        public static void Before(BlockEntityFarmland __instance, ref float __state)
        {
            if (__instance is not BlockEntityFarmlandV2 self) return;
            __state = self.moistureLevel;
        }

        [HarmonyPostfix]
        public static void After(BlockEntityFarmland __instance, ref float __state)
        {
            if (__instance is not BlockEntityFarmlandV2 self) return;
            float diff = __state - self.moistureLevel;
            self.moistureLevel += 0.25f * self.MulchLevel * diff;
        }
    }
}
