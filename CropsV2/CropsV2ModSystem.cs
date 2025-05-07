using HarmonyLib;
using Vintagestory.API.Common;

namespace Ehm93.VintageStory.CropsV2;

public class CropsV2ModSystem : ModSystem
{
  private Harmony patcher;
  
  public override void Start(ICoreAPI api)
  {
    base.Start(api);
    OverrideDefaultRecoverySpeed(api);
    RegisterTypes(api);
    HarmonyPatch();
  }
  
  public override void Dispose()
  {
    HarmonyUnpatch();
  }

  private void RegisterTypes(ICoreAPI api) {
    api.RegisterBlockClass("BlockCropV2", typeof(BlockCropV2));
    api.RegisterBlockEntityClass("BECropV2", typeof(BlockEntityCropV2));
    api.RegisterBlockEntityBehaviorClass("FarmlandMulch", typeof(BEBehaviorFarmlandMulch));
    api.RegisterItemClass("ItemPlantableSeedV2", typeof(ItemPlantableSeedV2));
  }

  private void OverrideDefaultRecoverySpeed(ICoreAPI api)
  {
    if (api.World.Config.TryGetFloat("fertilityRecoverySpeed") == null)
    {
      api.World.Config.SetFloat("fertilityRecoverySpeed", 0.05f);
    }
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
