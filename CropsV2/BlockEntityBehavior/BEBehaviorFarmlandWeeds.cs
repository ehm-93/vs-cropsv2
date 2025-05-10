using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BEBehaviorFarmlandWeeds : BlockEntityBehavior
{
    public BEBehaviorFarmlandWeeds(BlockEntity blockEntity) : base(blockEntity)
    {
        if (blockEntity is not BlockEntityFarmland) throw new ArgumentException("Configuration error! FarmlandWeeds behavior may only be assigned to farmland.");
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        var crop = Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(Pos.UpCopy());
        if (crop == null) return;

        var behavior = crop.GetBehavior<BEBehaviorCropWeeds>();
        if (behavior == null) return;

        var weedLevel = Math.Round(behavior.WeedLevel);
        if (0 < weedLevel) 
        {
            dsc.AppendLine(Lang.Get("Weeds: {0}%", weedLevel));
            var growthChance = Math.Round(behavior.WeedGrowthChance() * 100);
            if (growthChance != 0) dsc.AppendLine(Lang.Get("Weed growth chance: {0}%", growthChance));
        }
        else 
        {
            var sproutChance = Math.Round(behavior.WeedSproutChance() * 100);
            if (sproutChance != 0) dsc.AppendLine(Lang.Get("Weed sprout chance: {0}%", sproutChance));
        }
    }
}