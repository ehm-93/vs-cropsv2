using HarmonyLib;
using Vintagestory.API.Common;

namespace Ehm93.VintageStory.CropsV2;

public class CropsV2ModSystem : ModSystem
{
  private Harmony patcher;
  
  public override void Start(ICoreAPI api)
  {
    base.Start(api);
    api.RegisterBlockClass("BlockCropV2", typeof(BlockCropV2));
    api.RegisterBlockClass("BlockFarmlandV2", typeof(BlockFarmlandV2));

    api.RegisterBlockEntityClass("BECropV2", typeof(BlockEntityCropV2));
    api.RegisterBlockEntityClass("BEFarmlandV2", typeof(BlockEntityFarmlandV2));

    api.RegisterItemClass("ItemPlantableSeedV2", typeof(ItemPlantableSeedV2));
    
    HarmonyPatch();
  }
  
  public override void Dispose()
  {
    HarmonyUnpatch();
  }

  private void HarmonyPatch()
  { 
    // may duplicate if client and server share an instance
    if (!Harmony.HasAnyPatches(Mod.Info.ModID))
    {
      patcher = new Harmony(Mod.Info.ModID);
      patcher.PatchCategory(Mod.Info.ModID);
    }
  }

  private void HarmonyUnpatch()
  {
    patcher?.UnpatchAll(Mod.Info.ModID);
  }
}
