using System;
using Ehm93.VintageStory.CropsV2;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BEBehaviorFarmlandNutrients : BlockEntityBehavior
{
    // ~5 years for nutrition to grow from 0 -> 100
    private const float k = 0.000531f;
    private double lastCheckTotalHours = 0;
    private float[] nutrientRemainders = new float[3].Fill(0);

    public BlockEntityFarmland FarmlandEntity => (BlockEntityFarmland)Blockentity;

    public BEBehaviorFarmlandNutrients(BlockEntity blockentity) : base(blockentity)
    {
        if (blockentity is not BlockEntityFarmland) throw new ArgumentException($"Configuration error! BEBehaviorFarmlandAge may only be used against BlockEntityFarmland, found {blockentity.GetType().Name}");
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        if (api is ICoreServerAPI && api.World.Config.GetBool("processCrops", defaultValue: true))
        {
            Blockentity.RegisterGameTickListener(ServerTick, 25_000 + api.World.Rand.Next(10_000));
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetDouble("lastCheckTotalHours", lastCheckTotalHours);
        tree.SetFloat("nR0", nutrientRemainders[0]);
        tree.SetFloat("nR1", nutrientRemainders[1]);
        tree.SetFloat("nR2", nutrientRemainders[2]);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        lastCheckTotalHours = tree.TryGetDouble("lastCheckTotalHours") ?? 0;
        nutrientRemainders[0] = tree.GetFloat("nR0");
        nutrientRemainders[1] = tree.GetFloat("nR1");
        nutrientRemainders[2] = tree.GetFloat("nR2");
    }

    protected virtual void ServerTick(float df)
    {
        CheckNutrients();
    }

    protected virtual void CheckNutrients()
    {
        var prev = lastCheckTotalHours;
        var now = Api.World.Calendar.TotalHours;
        var deltaHours = now - prev;
        lastCheckTotalHours = now;

        if (HasCrop() || IsBlighted() || prev == 0)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            float current = FarmlandEntity.OriginalFertility[i] + nutrientRemainders[i];
            float updated = ComputeNutrients(current, (float)deltaHours);
            int floored = Math.Clamp((int)Math.Floor(updated), 0, 100);

            nutrientRemainders[i] = updated - floored;
            FarmlandEntity.OriginalFertility[i] = floored;
        }

        FarmlandEntity.MarkDirty();
    }

    protected virtual double BoostCoef()
    {
        var moisture = MoistureCoef(FarmlandEntity.MoistureLevel);
        var temp = TempCoef(Api.World.BlockAccessor.GetClimateAt(Pos).Temperature);
        var mulch = MulchCoef((FarmlandEntity.GetBehavior<BEBehaviorFarmlandMulch>()?.MulchLevel ?? 0) / 100);

        return moisture * temp * mulch;
    }

    protected virtual bool HasCrop()
    {
        return Api.World.BlockAccessor.GetBlock(Pos.UpCopy()) is BlockCrop;
    }

    protected virtual bool IsBlighted()
    {
        return (FarmlandEntity.GetBehavior<BEBehaviorFarmlandBlight>()?.SporeLevel ?? 0) > 0;
    }

    protected virtual float ComputeNutrients(float current, float deltaHours)
    {
        double effectiveK = k * BoostCoef();
        return 100f - (100f - current) * (float)Math.Exp(-effectiveK * deltaHours);
    }

    protected virtual double MoistureCoef(double m)
    {
        const double steepness = 12.0;    // Controls sharpness of rise
        const double midpoint = 0.65;     // Center of steepest ascent
        const double max = 2.0;

        return max * FunctionUtils.Sigmoid(m, midpoint, steepness);
    }

    protected virtual double TempCoef(double tempC)
    {
        if (tempC < 0) return 0.2f;
        if (tempC > 35) return 0.4f;
        return 0.2 + 0.8 * Math.Exp(-Math.Pow((tempC - 20) / 10.0, 2));
    }

    protected virtual double MulchCoef(double mulchiness)
    {
        return 1.0f + 0.5f * mulchiness; // up to +50%
    }
}