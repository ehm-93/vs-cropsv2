using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

internal static class BEBerryBushPatches
{
    public static void Patch(Harmony harmony, ILogger logger)
    {
        PatchBerryBushOnExchanged(harmony, logger);
        PatchBerryBushCheckGrow(harmony, logger);
        PatchBerryBushGetBlockInfo(harmony, logger);
    }

    private static void PatchBerryBushOnExchanged(Harmony harmony, ILogger logger)
    {
        var berryBushType = Herbarium.IsLoaded() ? Herbarium.BEGroundBerryPlantType() : typeof(BlockEntityBerryBush);
        var target = TryGetMethod(berryBushType, "OnExchanged", logger);
        var postfix = typeof(OnExchangedPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
        if (target != null && postfix != null)
        {
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            logger.Debug("Patched {0}.", target);
        }
    }

    private static void PatchBerryBushCheckGrow(Harmony harmony, ILogger logger)
    {
        var berryBushType = Herbarium.IsLoaded() ? Herbarium.BEBerryPlantType() : typeof(BlockEntityBerryBush);
        var target = TryGetMethod(berryBushType, "CheckGrow", logger);
        var prefix = typeof(CheckGrowPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
        if (target != null && prefix != null)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            logger.Debug("Patched {0}.", target);
        }
    }

    private static void PatchBerryBushGetBlockInfo(Harmony harmony, ILogger logger)
    {
        var berryBushType = Herbarium.IsLoaded() ? Herbarium.BEGroundBerryPlantType() : typeof(BlockEntityBerryBush);
        var target = TryGetMethod(berryBushType, "GetBlockInfo", logger);
        var postfix = typeof(GetBlockInfoPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
        if (target != null && postfix != null)
        {
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            logger.Debug("Patched {0}.", target);
        }
    }

    private static MethodInfo TryGetMethod(Type type, string name, ILogger logger)
    {
        try
        {
            return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        catch (AmbiguousMatchException ex)
        {
            logger.Warning($"Ambiguous match for method {name} in {type.FullName}: {ex.Message}");
            return null;
        }
    }

    public static class OnExchangedPatch
    {
        public static void Postfix(BlockEntity __instance, Block block)
        {
            var onExchanged = __instance.GetBehavior<OnExchanged>();
            if (onExchanged != null) onExchanged.OnExchanged(block);
        }
    }

    public static class CheckGrowPatch
    {
        public static bool Prefix(BlockEntity __instance)
        {
            var behaviors = __instance.Behaviors.Where(b => b is ICheckGrow);
            foreach (ICheckGrow behavior in behaviors)
            {
                if (!behavior.CheckGrow()) return false;
            }
            return true;
        }
    }

    internal static class GetBlockInfoPatch
    {
        public static void Postfix(BlockEntity __instance, IPlayer forPlayer, StringBuilder sb)
        {
            foreach (BlockEntityBehavior behavior in __instance.Behaviors)
            {
                behavior.GetBlockInfo(forPlayer, sb);
            }
        }
    }
}
