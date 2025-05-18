using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BlockBehaviorFarmlandInteractions : BlockBehavior
{
    protected ICoreAPI Api;
    private readonly Func<WorldInteraction[]> WeedInteractions;
    private readonly Func<WorldInteraction[]> MulchInteractions;
    private readonly Func<WorldInteraction[]> BlightInteractions;

    public BlockBehaviorFarmlandInteractions(Block block) : base(block)
    {
        if (block is not BlockFarmland) throw new ArgumentException($"Configuration error! BlockBehaviorFarmlandInteractions may only be used on BlockFarmland but found {block.GetType().Name}");

        WeedInteractions = FunctionUtils.Memoize(() =>
            {
                if (!WorldConfig.EnableWeeds) return new WorldInteraction[] { };

                var itemStacks = new List<ItemStack>();
                foreach (var item in Api.World.Items)
                {
                    if (item is ItemHoe)
                    {
                        itemStacks.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        Itemstacks = itemStacks.ToArray(),
                        ActionLangCode = "Weed",
                        MouseButton = EnumMouseButton.Right,
                    }
                };
            }
        );
        MulchInteractions = FunctionUtils.Memoize(() =>
            {
                if (!WorldConfig.EnableMulch) return new WorldInteraction[] { };

                var itemStacks = new List<ItemStack>();
                foreach (var item in Api.World.Items)
                {
                    if (item is ItemDryGrass)
                    {
                        itemStacks.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        Itemstacks = itemStacks.ToArray(),
                        ActionLangCode = "Mulch",
                        MouseButton = EnumMouseButton.Right,
                    }
                };
            }
        );
        BlightInteractions = FunctionUtils.Memoize(() =>
            {
                if (!WorldConfig.EnableBlight) return new WorldInteraction[] { };

                var itemStacks = new List<ItemStack>();
                foreach (var item in Api.World.Items)
                {
                    if (item.Code?.Path == "lime")
                    {
                        itemStacks.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        Itemstacks = itemStacks.ToArray(),
                        ActionLangCode = "Treat spores",
                        MouseButton = EnumMouseButton.Right,
                    }
                };
            }
        );
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        Api = api;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
    {
        var result = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);

        var farmlandEntity = world.BlockAccessor.GetBlockEntity(selection.Position);
        if (farmlandEntity is not null)
        {
            var mulchBehavior = farmlandEntity.GetBehavior<BEBehaviorFarmlandMulch>();
            if (mulchBehavior != null && mulchBehavior.MulchLevel < 100)
                result = result.Concat(MulchInteractions()).ToArray();

            var blightBehavior = farmlandEntity.GetBehavior<BEBehaviorFarmlandBlight>();
            if (blightBehavior != null && 0 < blightBehavior.SporeLevel && blightBehavior.SporeTreatment < 100)
                result = result.Concat(BlightInteractions()).ToArray();
        }

        var cropEntity = world.BlockAccessor.GetBlockEntity(selection.Position.UpCopy());
        if (cropEntity is not null)
        {
            var weedBehavior = cropEntity.GetBehavior<BEBehaviorCropWeeds>();
            if (weedBehavior != null && 0 < weedBehavior.WeedLevel)
                result = result.Concat(WeedInteractions()).ToArray();
        }

        return result;
    }
}