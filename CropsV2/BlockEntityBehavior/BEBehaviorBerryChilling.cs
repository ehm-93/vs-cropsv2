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
    protected readonly Func<bool> InGreenhouse;
    protected double chilledHours = 0;
    protected double lastCheckTotalHours = 0;
    protected bool chilling = false;
    protected double chillTemp = 0;
    protected double chilledHoursRequired = 0;
    private bool enabled = true;

    public bool Chilling
    {
        get { return chilling; }
        set
        {
            if (!chilling && value) chilledHours = 0;
            chilling = value;
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

        chillTemp = properties["chillTemp"]?.AsDouble() ?? 0;
        chilledHoursRequired = (properties["chilledDaysRequired"]?.AsDouble() ?? 0) * Api.World.Calendar.HoursPerDay;

        if (enabled && Api is ICoreServerAPI) Blockentity.RegisterGameTickListener(ServerTick, 4500 + Api.World.Rand.Next(1000));
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("chilledHours", chilledHours);
        tree.SetDouble("lastCheckTotalHours", lastCheckTotalHours);
        tree.SetBool("chilling", chilling);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        chilledHours = tree.TryGetDouble("chilledHours") ?? 0;
        lastCheckTotalHours = tree.TryGetDouble("lastCheckTotalHours") ?? 0;
        chilling = tree.TryGetBool("chilling") ?? false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (enabled && Chilling && ChillProgress < 1)
        {
            dsc.AppendLine(Lang.Get("Dormant"));
            dsc.AppendLine(Lang.Get("Vernalized below: {0}°C", chillTemp));
            dsc.AppendLine(Lang.Get("Vernalization progress: {0}%", Math.Round(ChillProgress * 100)));
        }
    }

    public virtual void OnExchanged(Block block)
    {
        Chilling = Block?.Variant?["state"] == "empty";
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

        if (!chilling || lastCheckTotalHours == 0)
        {
            lastCheckTotalHours = now;
            return;
        }

        double progressBefore = ChillProgress;
        double checkTime = lastCheckTotalHours;

        while (checkTime + intervalHours <= now)
        {
            checkTime += intervalHours;

            var temp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, checkTime).Temperature;
            temp += InGreenhouse() ? 5 : 0;
            if (temp <= chillTemp)
            {
                chilledHours += intervalHours;
            }
        }

        var tempNow = Api.World.BlockAccessor.GetClimateAt(Pos).Temperature + (InGreenhouse() ? 5 : 0);
        if (tempNow <= chillTemp)
        {
            chilledHours += now - checkTime;
        }

        lastCheckTotalHours = now;

        if (progressBefore != 1 && ChillProgress == 1)
        {
            Blockentity.MarkDirty(true);
        }
    }
}
