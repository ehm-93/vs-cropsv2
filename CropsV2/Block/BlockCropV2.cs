using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

public class BlockCropV2 : BlockCrop
{
    public int LastStage { get { return Attributes["lastStage"].AsInt(); } }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        var drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        
        var cropEntity = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCropV2;
        int gen = cropEntity?.Generation ?? 1;
        gen = GameMath.Clamp(gen, 1, 10);

        float yieldMultiplier = GetYieldMultiplier(gen);
        foreach (var drop in drops)
        {
            // don't reduce count for seeds
            if (drop.Item is ItemPlantableSeed)
            {
                drop.StackSize = (int) (Math.Max(yieldMultiplier, 1) * drop.StackSize);
            }
            else
            {
                drop.StackSize = (int) (yieldMultiplier * drop.StackSize);
            }
        }

        var nextGen = gen;
        if (Code.EndVariant().Equals(LastStage.ToString()))
        {
            nextGen++;
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

    // Exponential yield scaling
    // gen 1 -> 0.5
    // gen 5 -> 1.0
    // gen 10 -> 1.5
    // gen inf -> 2.0
    protected virtual float GetYieldMultiplier(int generation)
    {
        return -1.695f * (float)Math.Exp(-0.1221f * generation) + 2f;
    }
}
