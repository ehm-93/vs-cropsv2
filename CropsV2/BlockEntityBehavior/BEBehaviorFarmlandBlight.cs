using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BEBehaviorFarmlandBlight : BlockEntityBehavior
{
    public BEBehaviorFarmlandBlight(BlockEntity blockEntity) : base(blockEntity)
    {
        if (blockEntity is not BlockEntityFarmland) throw new ArgumentException("Configuration error! FarmlandWeeds behavior may only be assigned to farmland.");
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        var crop = Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(Pos.UpCopy());
        if (crop == null) return;

        var behavior = crop.GetBehavior<BEBehaviorCropBlight>();
        if (behavior == null) return;

        var blightLevel = Math.Round(behavior.BlightLevel);
        if (0 < blightLevel) 
        {
            dsc.AppendLine(Lang.Get("Blight: {0}%", blightLevel));
        }
        else 
        {
            var chance = Math.Round(behavior.OutbreakChance() * 100);
            if (0 < chance) dsc.AppendLine(Lang.Get("Blight risk: {0}%", chance));
        }
    }
}