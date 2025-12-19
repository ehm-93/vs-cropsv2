using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BEBehaviorBerryChilling : BlockEntityBehavior, ICheckGrow, OnExchanged
{
    // memoized function to check if the block is in a greenhouse
    protected readonly Func<bool> InGreenhouse;
    // number of accrued chilled hours
    protected double chilledHours = 0;
    // last check time in total hours
    protected double lastCheckTotalHours = 0;
    // the temperature below which vernalization occurs
    protected double chillTemp = 0;
    // number of chilled hours required for vernalization
    protected double chilledHoursRequired = 0;
    // devernalization threshold (fraction of chilled hours progress)
    // example: 0.5 means that if the chill progress is below 50%
    //          then devernalization can occur
    protected double devernalizationThreshold = 0.50;
    // temperature above which devernalization occurs
    protected double devernalizationTemperature = 0;
    // fraction of chilled hours retained per hour above devernalizationTemperature
    protected double devernalizationFactor = 0.6667;
    // temperature above which devernalization always occurs, ignoring threshold
    protected double forceDevernalizationTemperature = 0;
    // fraction of chilled hours retained per hour above forceDevernalizationTemperature
    protected double forceDevernalizationFactor = 0;
    private bool enabled = true;

    public bool Chilling
    {
        get { return ChillProgress < 1; }
        set
        {
            if (Chilling == value) return;
            if (!Chilling) chilledHours = 0;
            else chilledHours = chilledHoursRequired;
            Blockentity.MarkDirty(true);
        }
    }

    public double ChillProgress => Math.Clamp(chilledHours / chilledHoursRequired, 0, 1);

    public bool Enabled => enabled;

    public BEBehaviorBerryChilling(BlockEntity blockentity) : base(blockentity)
    {
        InGreenhouse = FunctionUtils.MemoizeFor(
            TimeSpan.FromMinutes(2),
            () =>
            {
                var rooms = Api.ModLoader.GetModSystem<RoomRegistry>();
                var room = rooms.GetRoomForPosition(Pos);
                return room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0;
            }
        );
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        enabled = WorldConfig.EnableBerryVernalization;

        chillTemp = properties["chillTemp"].AsDoubleOrDefault(chillTemp);
        chilledHoursRequired = properties["chilledDaysRequired"].AsDoubleOrDefault(chilledHoursRequired / Api.World.Calendar.HoursPerDay) * Api.World.Calendar.HoursPerDay;
        devernalizationThreshold = properties["devernalizationThreshold"].AsDoubleOrDefault(devernalizationThreshold);
        devernalizationTemperature = properties["devernalizationTemperature"].AsDoubleOrDefault(chillTemp + 3);
        devernalizationFactor = properties["devernalizationFactor"].AsDoubleOrDefault(devernalizationFactor);
        forceDevernalizationTemperature = properties["forceDevernalizationTemperature"].AsDoubleOrDefault(devernalizationTemperature + 5);
        forceDevernalizationFactor = properties["forceDevernalizationFactor"].AsDoubleOrDefault(forceDevernalizationFactor);

        if (Block.Variant?["state"] == "ripe") Chilling = false;

        if (enabled && Api is ICoreServerAPI) Blockentity.RegisterGameTickListener(ServerTick, 4500 + Api.World.Rand.Next(1000));
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("chilledHours", chilledHours);
        tree.SetDouble("lastCheckTotalHours", lastCheckTotalHours);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        chilledHours = tree.TryGetDouble("chilledHours") ?? 0;
        lastCheckTotalHours = tree.TryGetDouble("lastCheckTotalHours") ?? 0;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (enabled && Chilling)
        {
            dsc.Clear();
            dsc.AppendLine(Lang.Get("Dormant"));
            dsc.AppendLine(Lang.Get("Vernalized below: {0}°C", chillTemp));
            dsc.AppendLine(Lang.Get("Vernalization progress: {0}%", Math.Round(ChillProgress * 100)));
        }
    }

    public virtual void OnExchanged(Block block)
    {
        if (Block == block) return;
        if (block?.Variant?["state"] == "empty") Chilling = true;
    }

    public virtual bool CheckGrow()
    {
        if (!enabled) return true;

        return !Chilling || 1 <= ChillProgress;
    }

    protected virtual void ServerTick(float df)
    {
        CheckChill();
    }

    protected virtual void CheckChill()
    {
        const double intervalHours = 2.0;

        var now = Api.World.Calendar.TotalHours;

        if (!Chilling || lastCheckTotalHours == 0)
        {
            lastCheckTotalHours = now;
            return;
        }

        double progressBefore = ChillProgress;
        double checkTime = lastCheckTotalHours;

        while (checkTime + intervalHours <= now)
        {
            checkTime += intervalHours;

            var temp = Api.World.BlockAccessor.GetClimateAt(
                Pos,
                EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
                checkTime / Api.World.Calendar.HoursPerDay
            ).Temperature;
            temp += InGreenhouse() ? 5 : 0;
            if (temp <= chillTemp)
            {
                chilledHours += intervalHours;
            }
            else if (temp > forceDevernalizationTemperature)
            {
                chilledHours *= Math.Pow(forceDevernalizationFactor, intervalHours);
            }
            else if (temp > devernalizationTemperature && ChillProgress < devernalizationThreshold)
            {
                chilledHours *= Math.Pow(devernalizationFactor, intervalHours);
            }
        }

        var tempNow = Api.World.BlockAccessor.GetClimateAt(Pos).Temperature + (InGreenhouse() ? 5 : 0);
        var remainingHours = now - checkTime;
        if (tempNow <= chillTemp)
        {
            chilledHours += remainingHours;
        }
        else if (tempNow > forceDevernalizationTemperature)
        {
            chilledHours *= Math.Pow(forceDevernalizationFactor, remainingHours);
        }
        else if (tempNow > devernalizationTemperature && ChillProgress < devernalizationThreshold)
        {
            chilledHours *= Math.Pow(devernalizationFactor, remainingHours);
        }

        lastCheckTotalHours = now;

        if (progressBefore != ChillProgress)
        {
            Blockentity.MarkDirty(true);
        }
    }
}
