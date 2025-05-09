using System;

namespace Ehm93.VintageStory.CropsV2;

internal class PrimitiveSurvival
{
    public static bool IsLoaded()
    {
        if (Type.GetType("PrimitiveSurvival.ModSystem.PrimitiveSurvivalSystem, primitivesurvival") == null)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public static Type HoeType()
    {
        return Type.GetType("PrimitiveSurvival.ModSystem.ItemHoeExtended, primitivesurvival");
    }
}
