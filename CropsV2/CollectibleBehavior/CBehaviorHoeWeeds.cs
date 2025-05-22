using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

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

        var behavior = FindCropWeedBehavior(blockSel.Position);
        if (behavior == null) return;

        var lvlBefore = behavior.WeedLevel;
        behavior.WeedLevel -= HoeImpact();
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

    private BEBehaviorCropWeeds FindCropWeedBehavior(BlockPos pos)
    {
        var entity = Api.World.BlockAccessor.GetBlockEntity(pos);
        if (entity == null) return null;
        if (entity is BlockEntityFarmland) entity = Api.World.BlockAccessor.GetBlockEntity(pos.UpCopy());
        if (entity is not BlockEntityCropV2) return null;
        return entity.GetBehavior<BEBehaviorCropWeeds>();
    }

    private double HoeImpact()
    {
        return Hoe.Code.EndVariant() switch
        {
            "flint" => 15,
            "obsidian" => 15,
            "copper" => 20,
            "tinbronze" => 25,
            "bismuthbronze" => 25,
            "blackbronze" => 25,
            "iron" => 35,
            "meteoriciron" => 35,
            "steel" => 50,
            _ => 10
        };
    }
}
