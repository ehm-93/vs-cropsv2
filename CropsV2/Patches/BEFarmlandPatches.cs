using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

internal class BEFarmlandPatches
{
    [HarmonyPatchCategory("cropsv2")]
    [HarmonyPatch(typeof(BlockEntityFarmland), "GetBlockInfo")]
    internal static class GetBlockInfoPatch
    {
        [HarmonyPostfix]
        public static void After(BlockEntityFarmland __instance, IPlayer forPlayer, StringBuilder dsc)
        {
            foreach (BlockEntityBehavior behavior in __instance.Behaviors)
            {
                behavior.GetBlockInfo(forPlayer, dsc);
            }
        }
    }

    [HarmonyPatchCategory("cropsv2")]
    [HarmonyPatch(typeof(BlockEntityFarmland), "OnTesselation")]
    internal static class OnTesselationPatch
    {
        [HarmonyPostfix]
        public static void After(BlockEntityFarmland __instance, ref bool __result, ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            bool flag = false;
            for (int i = 0; i < __instance.Behaviors.Count; i++)
            {
                flag |= __instance.Behaviors[i].OnTesselation(mesher, tessThreadTesselator);
            }
            __result |= flag;
        }
    }

    [HarmonyPatchCategory("cropsv2")]
    [HarmonyPatch(typeof(BlockEntityFarmland), "updateMoistureLevel", new Type[] {
        typeof(double), typeof(float), typeof(bool), typeof(ClimateCondition)
    })]
    internal static class UpdateMoistureLevelPatch
    {
        static private FieldInfo moistureLevel = AccessTools.Field(typeof(BlockEntityFarmland), "moistureLevel");

        [HarmonyPrefix]
        public static void Before(BlockEntityFarmland __instance, ref float __state)
        {
            var behavior = __instance.GetBehavior<BEBehaviorFarmlandMulch>();
            if (behavior == null) return;

            // remmber moisture level before mutated by base class
            __state = __instance.MoistureLevel;
        }

        [HarmonyPostfix]
        public static void After(BlockEntityFarmland __instance, ref float __state)
        {
            var behavior = __instance.GetBehavior<BEBehaviorFarmlandMulch>();
            if (behavior == null) return;
            
            // slow down moisture loss depending on mulch level
            var diff = __state - __instance.MoistureLevel;
            var mulchCoef = 0.0075f * behavior.MulchLevel;
            if (diff > 0) 
            {
                var newVal = __instance.MoistureLevel + (float) mulchCoef * diff;
                moistureLevel.SetValue(__instance, newVal);
            }
        }
    }

    [HarmonyPatchCategory("cropsv2")]
    [HarmonyPatch(typeof(BlockEntityFarmland), "OnBlockInteract")]
    internal static class OnBlockInteractPatch
    {
        [HarmonyPrefix]
        public static bool Before(BlockEntityFarmland __instance, ref bool __result, IPlayer byPlayer)
        {
            var behavior = __instance.GetBehavior<BEBehaviorFarmlandMulch>();
            if (behavior == null) return true;

            var handled = behavior.OnBlockInteract(byPlayer);
            __result = handled;
            return !handled;
        }
    }
}

