using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Ehm93.VintageStory.CropsV2;

static class WorldConfig
{
    public const string EnableBerryVernalizationKey = "cropsv2:enableBerryVernalization";
    public const string EnableBlightKey = "cropsv2:enableBlight";
    public const string EnableSporesKey = "cropsv2:enableSpores";
    public const string EnableWeedsKey = "cropsv2:enableWeeds";
    public const string EnableFarmlandAgingKey = "cropsv2:enableFarmlandAging";
    public const string FarmlandAgingRateKey = "cropsv2:farmlandAgingRate";
    public const string FarmlandAgingMaxKey = "cropsv2:farmlandAgingMax";
    public const string EnableCropGenerationsKey = "cropsv2:enableCropGenerations";
    public const string EnableMulchKey = "cropsv2:enableMulch";

    private static ICoreAPI api;
    public static bool EnableBerryVernalization => api.World.Config.GetBool(EnableBerryVernalizationKey, true);
    public static bool EnableBlight => api.World.Config.GetBool(EnableBlightKey, true);
    public static bool EnableSpores => api.World.Config.GetBool(EnableSporesKey, true);
    public static bool EnableWeeds => api.World.Config.GetBool(EnableWeedsKey, true);
    public static bool EnableFarmlandAging => api.World.Config.GetBool(EnableFarmlandAgingKey, true);
    public static float FarmlandAgingRate => api.World.Config.GetFloat(FarmlandAgingRateKey, 0.0001f);
    public static float FarmlandAgingMax => api.World.Config.GetFloat(FarmlandAgingMaxKey, 65f);
    public static bool EnableCropGenerations => api.World.Config.GetBool(EnableCropGenerationsKey, true);
    public static bool EnableMulch => api.World.Config.GetBool(EnableMulchKey, true);
    

    public static void Init(ICoreAPI api)
    {
        WorldConfig.api = api;
    }
}