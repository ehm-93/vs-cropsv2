using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Ehm93.VintageStory.CropsV2;

public class CropsV2ModSystem : ModSystem
{
  private Harmony patcher;

  public override void Start(ICoreAPI api)
  {
    base.Start(api);
    WorldConfig.Init(api);
    CropsV2Commands.Register(api);
    RegisterTypes(api);
    HarmonyPatch(api);
  }

  public override void Dispose()
  {
    HarmonyUnpatch();
  }

  public override void AssetsFinalize(ICoreAPI api)
  {
    // https://github.com/gabriella-campos-davis/Herbarium/pull/20
    if (!Herbarium.IsLoaded()) RegisterBerryChilling(api);

    base.AssetsFinalize(api);
  }

  private void RegisterTypes(ICoreAPI api) {
    api.RegisterBlockClass("BlockCropV2", typeof(BlockCropV2));
    api.RegisterBlockBehaviorClass("FarmlandInteractions", typeof(BlockBehaviorFarmlandInteractions));
    api.RegisterCropBehavior("CropWeeds", typeof(CropBehaviorWeeds));
    api.RegisterCropBehavior("CropBlight", typeof(CropBehaviorBlight));
    api.RegisterBlockEntityClass("BECropV2", typeof(BlockEntityCropV2));
    api.RegisterBlockEntityBehaviorClass("FarmlandMulch", typeof(BEBehaviorFarmlandMulch));
    api.RegisterBlockEntityBehaviorClass("FarmlandWeeds", typeof(BEBehaviorFarmlandWeeds));
    api.RegisterBlockEntityBehaviorClass("FarmlandBlight", typeof(BEBehaviorFarmlandBlight));
    api.RegisterBlockEntityBehaviorClass("FarmlandNutrients", typeof(BEBehaviorFarmlandNutrients));
    api.RegisterBlockEntityBehaviorClass("CropWeeds", typeof(BEBehaviorCropWeeds));
    api.RegisterBlockEntityBehaviorClass("CropBlight", typeof(BEBehaviorCropBlight));
    // https://github.com/gabriella-campos-davis/Herbarium/pull/20
    if (!Herbarium.IsLoaded()) api.RegisterBlockEntityBehaviorClass("BerryChilling", typeof(BEBehaviorBerryChilling));
    api.RegisterItemClass("ItemPlantableSeedV2", typeof(ItemPlantableSeedV2));
    api.RegisterCollectibleBehaviorClass("HoeWeeds", typeof(CBehaviorHoeWeeds));
  }

  private void RegisterBerryChilling(ICoreAPI api)
  {
    if (api.Side != EnumAppSide.Server) {
        return;
    }

    foreach (var b in api.World.Blocks)
    {
        if (b.Code.Domain != "game") continue;
        if (!b.Code.Path.StartsWith("bigberrybush-") || b.Code.Path.StartsWith("smallberrybush-")) continue;

        b.EntityClass ??= "Generic";
        b.BlockEntityBehaviors = [
            ..b.BlockEntityBehaviors,
            new BlockEntityBehaviorType
            {
                Name = "BerryChilling",
                properties = JsonObject.FromJson("{\"chillTemp\":7,\"chilledDaysRequired\":10}")
            },
        ];
    }
  }

  private void HarmonyPatch(ICoreAPI api)
  {
    // may duplicate if client and server share an instance
    if (!Harmony.HasAnyPatches(Mod.Info.ModID))
    {
      patcher = new Harmony(Mod.Info.ModID);
      patcher.PatchCategory(Mod.Info.ModID);
      HoePatches.Patch(patcher, api.Logger);
      BEBerryBushPatches.Patch(patcher, api.Logger);
    }
  }

  private void HarmonyUnpatch()
  {
    patcher?.UnpatchAll(Mod.Info.ModID);
  }
}
