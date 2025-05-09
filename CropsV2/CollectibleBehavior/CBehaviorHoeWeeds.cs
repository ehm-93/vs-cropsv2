using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

// TODO: support weed clearing when interact with both farmland or crop

class CBehaviorHoeWeeds : CollectibleBehavior
{
    protected ICoreAPI Api;

    public ItemHoe Hoe {
        get { return (ItemHoe) collObj; }
    }

    public CBehaviorHoeWeeds(CollectibleObject collObj) : base(collObj)
    {
        if (collObj is not ItemHoe) throw new ArgumentException("Configuration error! HoeWeeds behavior may only be used on hoes!");
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        Api = api;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);

        if (blockSel == null) return;

        var entity = Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(blockSel.Position);
        if (entity == null) return;

        var behavior = entity.GetBehavior<BEBehaviorCropWeeds>();
        if (behavior == null) return;

        var lvlBefore = behavior.WeedLevel;
        behavior.WeedLevel -= 25;
        if (lvlBefore != behavior.WeedLevel)
        {
            Api.World.PlaySoundAt(
                new AssetLocation("cropsv2:sounds/weeds/hoe"),
                blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                (byEntity as EntityPlayer).Player,
                randomizePitch: true,
                range: 8
            );
            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, (byEntity as EntityPlayer).Player.InventoryManager.ActiveHotbarSlot);
            if (slot.Empty)
            {
                byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"), byEntity.Pos.X, byEntity.Pos.InternalY, byEntity.Pos.Z);
            }
        }

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
    }
    
    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }
}