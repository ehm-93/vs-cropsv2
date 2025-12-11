using System;

namespace Ehm93.VintageStory.CropsV2;

static class Herbarium
{
    public static bool IsLoaded()
    {
        if (Type.GetType("herbarium.Herbarium, Herbarium") == null) return false;
        else return true;
    }
}
