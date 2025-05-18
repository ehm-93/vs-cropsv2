using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

public class BlockCropV2 : BlockCrop
{
    private readonly Func<WorldInteraction[]> WeedInteractions;
    private readonly Func<WorldInteraction[]> MulchInteractions;
    private readonly Func<WorldInteraction[]> BlightInteractions;
    private bool enabled = true;

    public BlockCropV2()
    {
        WeedInteractions = FunctionUtils.Memoize(() =>
            {
                if (!WorldConfig.EnableWeeds) return new WorldInteraction[] { };

                var itemStacks = new List<ItemStack>();
                foreach (var item in api.World.Items)
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
                foreach (var item in api.World.Items)
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
                foreach (var item in api.World.Items)
                {
                    if (item.Code.Path == "lime")
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
        enabled = WorldConfig.EnableCropGenerations;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        var drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

        if (!enabled) return drops;

        var cropEntity = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCropV2;
        int gen = cropEntity?.Generation ?? 0;

        float yieldMultiplier = GetYieldMultiplier(gen);
        foreach (var drop in drops)
        {
            // Use a clamped multiplier for seeds to avoid yield loss
            float effectiveMultiplier = (drop.Item is ItemPlantableSeed)
                ? Math.Max(yieldMultiplier, 1f)
                : yieldMultiplier;

            double scaled = effectiveMultiplier * drop.StackSize;
            int baseAmount = (int)Math.Floor(scaled);
            double fractional = scaled - baseAmount;

            if (world.Rand.NextDouble() < fractional)
            {
                baseAmount += 1;
            }

            drop.StackSize = baseAmount;
        }

        var nextGen = gen;
        if (IsRipe())
        {
            // add 1 if the crop completed growth
            nextGen++;
        }
        BlockEntityFarmland blockEntityFarmland = world.BlockAccessor.GetBlockEntity(pos.DownCopy()) as BlockEntityFarmland;
        if (blockEntityFarmland == null)
        {
            // wild crops always drop gen 0
            nextGen = 0;
        }

        foreach (var drop in drops)
        {
            // propagate gen to seed drops
            if (drop.Item is ItemPlantableSeed)
            {
                drop.Attributes.SetInt("generation", nextGen);
            }
        }

        return drops;
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        var info = base.GetPlacedBlockInfo(world, pos, forPlayer);

        if (!enabled) return info;

        var entity = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCropV2;
        if (entity == null) return info;
        if (entity.Generation != 0)
        {
            return info + "\n" + Lang.Get("Generation: {0}", entity.Generation);
        }
        else
        {
            return info;
        }
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        var result = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

        var cropEntity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(selection.Position);
        if (cropEntity != null)
        {
            var weedBehavior = cropEntity.GetBehavior<BEBehaviorCropWeeds>();
            if (weedBehavior != null) result = weedBehavior.WeedLevel > 0 ? result.Concat(WeedInteractions()).ToArray() : result;
        }

        var farmlandEntity = api.World.BlockAccessor.GetBlockEntity<BlockEntityFarmland>(selection.Position.DownCopy());
        if (farmlandEntity != null)
        {
            var mulchBehavior = farmlandEntity.GetBehavior<BEBehaviorFarmlandMulch>();
            if (mulchBehavior != null) result = mulchBehavior.MulchLevel < 100 ? result.Concat(MulchInteractions()).ToArray() : result;

            var blightBehavior = farmlandEntity.GetBehavior<BEBehaviorFarmlandBlight>();
            if (blightBehavior != null && 0 < blightBehavior.SporeLevel && blightBehavior.SporeTreatment < 100)
                result = result.Concat(BlightInteractions()).ToArray();
        }

        return result;
    }

    // Exponential yield scaling
    // gen 1 -> 0.5
    // gen 5 -> 1.0
    // gen 10 -> 1.5
    // gen inf -> 2.0
    protected virtual float GetYieldMultiplier(int generation)
    {
        return -1.695f * (float)Math.Exp(-0.1221f * generation) + 2f;
    }

    protected int GetCropStage()
    {
        int.TryParse(LastCodePart(), out var result);
        return result;
    }

    protected bool IsRipe()
    {
        return GetCropStage() >= CropProps.GrowthStages;
    }
}
