using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class CropBehaviorWeeds : CropBehavior
{
    private bool enabled = true;


    public CropBehaviorWeeds(Block block) : base(block) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        enabled = WorldConfig.EnableWeeds;
    }

    public override bool TryGrowCrop(ICoreAPI api, IFarmlandBlockEntity farmland, double currentTotalHours, int newGrowthStage, ref EnumHandling handling)
    {
        if (!enabled) return true;

        BlockPos pos = farmland.UpPos;
        double weedLevel = GetWeedLevel(api, pos);        // 0–100
        double maturity = GetCropMaturity(api, pos);      // 0.0–1.0
        int generation = GetCropGeneration(api, pos);     // 1–10+

        // Later generations are more sensitive to weeds
        double genFactor = Sigmoid(generation, 5, 0.7);              // 0.5 at gen 5, 0.9+ by gen 8+

        // Less mature crops are more vulnerable
        double maturityFactor = 1.0 - Sigmoid(maturity, 0.5, 10);    // 1.0 at sprout, ~0 at maturity

        // Final skip chance
        double skipChance = weedLevel / 100.0 * genFactor * maturityFactor;

        if (api.World.Rand.NextDouble() < skipChance)
        {
            handling = EnumHandling.PreventSubsequent;
            return false;
        }

        return true;
    }

    private double GetWeedLevel(ICoreAPI api, BlockPos pos)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity(pos);
        if (entity == null) return 0;

        var behavior = entity.GetBehavior<BEBehaviorCropWeeds>();
        return behavior?.WeedLevel ?? 0;
    }

    private int GetCropGeneration(ICoreAPI api, BlockPos pos)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(pos);
        return entity?.Generation ?? 0;
    }

    private double GetCropMaturity(ICoreAPI api, BlockPos pos)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(pos);
        if (entity == null) return 0;

        return (double)CropStage() / CropFinalStage();
    }

    private int CropStage()
    {
        if (block is not BlockCrop crop) return 1;
        int.TryParse(crop.LastCodePart(), out var result);
        return result;
    }

    private int CropFinalStage()
    {
        if (block is not BlockCrop crop) return 1;
        return crop.CropProps.GrowthStages;
    }

    private double Sigmoid(double x, double center, double k) =>
        1.0 / (1.0 + Math.Exp(-k * (x - center)));
}
