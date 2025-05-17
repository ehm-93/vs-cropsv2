using System;

namespace Ehm93.VintageStory.CropsV2;

static class Herbarium
{
    public static bool IsLoaded()
    {
        if (Type.GetType("herbarium.Herbarium, Herbarium") == null) return false;
        else return true;
    }

    public static Type BEBerryPlantType()
    {
        return Type.GetType("herbarium.BEBerryPlant, Herbarium");
    }

    public static Type BEGroundBerryPlantType()
    {
        return Type.GetType("herbarium.BEGroundBerryPlant, Herbarium");
    }

    public static Type BEHerbariumBerryBushType()
    {
        return Type.GetType("herbarium.BEHerbariumBerryBush, Herbarium");
    }

    public static Type BEBehaviorBerryPlantType()
    {
        return Type.GetType("herbarium.BEHerbariumBerryBush, Herbarium");
    }
}