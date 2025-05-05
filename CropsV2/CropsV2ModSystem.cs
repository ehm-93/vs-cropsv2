using Vintagestory.API.Common;

namespace Ehm93.VintageStory.CropsV2;

public class CropsV2ModSystem : ModSystem
{
  public override void Start(ICoreAPI api)
  {
    base.Start(api);
    api.RegisterBlockClass("BlockCropV2", typeof(BlockCropV2));
    api.RegisterBlockEntityClass("BECropV2", typeof(BlockEntityCropV2));
    api.RegisterItemClass("ItemPlantableSeedV2", typeof(ItemPlantableSeedV2));
  }
}
