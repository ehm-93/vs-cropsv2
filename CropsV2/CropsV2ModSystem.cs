using HarmonyLib;
using Vintagestory.API.Common;

namespace Ehm93.VintageStory.CropsV2;

public class CropsV2ModSystem : ModSystem
{
  private Harmony patcher;
  
  public override void Start(ICoreAPI api)
  {
    base.Start(api);
    CropsV2Commands.Register(api);
    RegisterTypes(api);
    HarmonyPatch(api);
  }
  
  public override void Dispose()
  {
    HarmonyUnpatch();
  }

  private void RegisterTypes(ICoreAPI api) {
    api.RegisterBlockClass("BlockCropV2", typeof(BlockCropV2));
    api.RegisterCropBehavior("CropWeeds", typeof(CropBehaviorWeeds));
    api.RegisterBlockEntityClass("BECropV2", typeof(BlockEntityCropV2));
    api.RegisterBlockEntityBehaviorClass("FarmlandMulch", typeof(BEBehaviorFarmlandMulch));
    api.RegisterBlockEntityBehaviorClass("FarmlandWeeds", typeof(BEBehaviorFarmlandWeeds));
    api.RegisterBlockEntityBehaviorClass("CropWeeds", typeof(BEBehaviorCropWeeds));
    api.RegisterItemClass("ItemPlantableSeedV2", typeof(ItemPlantableSeedV2));
    api.RegisterCollectibleBehaviorClass("HoeWeeds", typeof(CBehaviorHoeWeeds));
  }

  private void HarmonyPatch(ICoreAPI api)
  { 
    // may duplicate if client and server share an instance
    if (!Harmony.HasAnyPatches(Mod.Info.ModID))
    {
      patcher = new Harmony(Mod.Info.ModID);
      patcher.PatchCategory(Mod.Info.ModID);
      HoePatches.Patch(patcher, api.Logger);
    }
  }

  private void HarmonyUnpatch()
  {
    patcher?.UnpatchAll(Mod.Info.ModID);
  }
}
