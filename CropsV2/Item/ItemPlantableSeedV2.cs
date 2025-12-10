using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

public class ItemPlantableSeedV2 : ItemPlantableSeed
{
    public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
    {
        if (blockSel == null)
        {
            return;
        }

        BlockPos position = blockSel.Position;
        string text = itemslot.Itemstack.Collectible.LastCodePart();
        if (text == "bellpepper")
        {
            return;
        }

        BlockEntity blockEntity = byEntity.World.BlockAccessor.GetBlockEntity(position);
        if (!(blockEntity is BlockEntityFarmland))
        {
            return;
        }

        Block block = byEntity.World.GetBlock(CodeWithPath("crop-" + text + "-1"));
        if (block == null)
        {
            return;
        }

        IPlayer player = null;
        if (byEntity is EntityPlayer)
        {
            player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
        }

        bool num = ((BlockEntityFarmland)blockEntity).TryPlant(block, itemslot, byEntity, blockSel);
        if (num)
        {
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), position, 0.4375, player);
            ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            if (player == null || (player.WorldData?.CurrentGameMode).GetValueOrDefault() != EnumGameMode.Creative)
            {
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
            }

            var seedGen = itemslot.Itemstack?.Attributes?.GetInt("generation") ?? 0;
            var cropEntity = byEntity.World.BlockAccessor.GetBlockEntity(position.UpCopy()) as BlockEntityCropV2;
            if (cropEntity != null)
            {
                cropEntity.Generation = seedGen;
            }
        }

        if (num)
        {
            handHandling = EnumHandHandling.PreventDefault;
        }
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        var gen = inSlot.Itemstack?.Attributes?.GetInt("generation");
        if (gen != null && gen != 0)
        {
            dsc.AppendLine(Lang.Get("Generation: {0}", gen));
        }
    }
}
