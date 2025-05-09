using System;
using System.Reflection;
using Ehm93.VintageStory.CropsV2;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

internal class HoePatches
{
    public static void Patch(Harmony harmony, ILogger logger)
    {
        var hoeType = HoeType();
        PatchHoeOnLoaded(harmony, logger, hoeType);
        PatchHoeOnHeldInteractStart(harmony, logger, hoeType);
        PatchHoeOnHeldInteractStep(harmony, logger, hoeType);
    }

    private static Type HoeType()
    {
        Type hoeType = null;
        if (PrimitiveSurvival.IsLoaded()) hoeType = PrimitiveSurvival.HoeType();
        if (hoeType == null) hoeType = typeof(ItemHoe);
        return hoeType;
    }

    private static void PatchHoeOnLoaded(Harmony harmony, ILogger logger, Type hoeType)
    {
        var target = TryGetMethod(hoeType, "OnLoaded", logger);
        var prefix = typeof(OnLoadedPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
        if (target != null && prefix != null)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            logger.Debug("Patched {0}.", target);
        }
    }

    private static void PatchHoeOnHeldInteractStart(Harmony harmony, ILogger logger, Type hoeType)
    {
        var target = TryGetMethod(hoeType, "OnHeldInteractStart", logger);
        var prefix = typeof(OnHeldInteractStartPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
        if (target != null && prefix != null)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            logger.Debug("Patched {0}.", target);
        }
    }

    private static void PatchHoeOnHeldInteractStep(Harmony harmony, ILogger logger, Type hoeType)
    {
        var target = TryGetMethod(hoeType, "OnHeldInteractStep", logger);
        var prefix = typeof(OnHeldInteractStepPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
        if (target != null && prefix != null)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
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

    internal class OnLoadedPatch
    {
        public static void Prefix(Item __instance, ICoreAPI api)
        {
            foreach (var behavior in __instance.CollectibleBehaviors)
            {
                behavior.OnLoaded(api);
            }
        }
    }

    internal class OnHeldInteractStartPatch
    {
        public static bool Prefix(Item __instance, ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            EnumHandHandling localHandling = EnumHandHandling.NotHandled;
            bool handled = false;

            foreach (var behavior in __instance.CollectibleBehaviors)
            {
                EnumHandling behaviorHandling = EnumHandling.PassThrough;
                behavior.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref localHandling, ref behaviorHandling);

                if (behaviorHandling != EnumHandling.PassThrough)
                {
                    handHandling = localHandling;
                    handled = true;
                }

                if (behaviorHandling == EnumHandling.PreventSubsequent)
                    return false;
            }

            return !handled;
        }
    }

    internal class OnHeldInteractStepPatch
    {
        public static bool Prefix(Item __instance, ref bool __result, float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            bool finalResult = true;
            bool anyHandled = false;

            foreach (var behavior in __instance.CollectibleBehaviors)
            {
                EnumHandling handling = EnumHandling.PassThrough;
                bool result = behavior.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);

                if (handling != EnumHandling.PassThrough)
                {
                    finalResult &= result;
                    anyHandled = true;
                }

                if (handling == EnumHandling.PreventSubsequent)
                {
                    __result = finalResult;
                    return false;
                }
            }

            if (anyHandled)
            {
                __result = finalResult;
                return false;
            }

            return true;
        }
    }
}