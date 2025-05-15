using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Ehm93.VintageStory.CropsV2;

class CropBehaviorBlight : CropBehavior
{
    public CropBehaviorBlight(Block block) : base(block) {}

    public override bool TryGrowCrop(ICoreAPI api, IFarmlandBlockEntity farmland, double currentTotalHours, int newGrowthStage, ref EnumHandling handling)
    {
        BlockPos pos = farmland.UpPos;
        double blightLevel = GetBlightLevel(api, pos);

        if (0.5 < blightLevel)
        {
            handling = EnumHandling.PreventSubsequent;
            return false;
        }

        return true;
    }

    private double GetBlightLevel(ICoreAPI api, BlockPos pos)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity(pos);
        if (entity == null) return 0;

        var behavior = entity.GetBehavior<BEBehaviorCropBlight>();
        return behavior?.BlightLevel ?? 0;
    }
}
