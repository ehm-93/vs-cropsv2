using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BlockFarmlandV2 : BlockFarmland
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
        if (entity is BlockEntityFarmlandV2 blockEntityFarmland && blockEntityFarmland.OnBlockInteractV2(byPlayer))
        {
            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
