using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BEBehaviorFarmlandBlight : BlockEntityBehavior
{
    protected List<CropHistoryEntry> history = new();
    protected double sporeLevel = 0;
    protected double lastCheckTotalHours = 0;
    protected SimpleParticleProperties blightParticles;
    public BlockEntityFarmland FarmlandEntity => Blockentity as BlockEntityFarmland;

    public BlockEntityCropV2 CropEntity =>
        Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(Pos.UpCopy());

    public BEBehaviorFarmlandBlight(BlockEntity blockEntity) : base(blockEntity)
    {
        if (blockEntity is not BlockEntityFarmland) throw new ArgumentException("Configuration error! FarmlandBlight behavior may only be assigned to farmland.");
    }

    public double SporeLevel
    {
        get { return sporeLevel; }
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (clamped != sporeLevel)
            {
                sporeLevel = clamped;
                CropEntity?.MarkDirty(true);
                FarmlandEntity.MarkDirty(true);
            }
        }
    }

    public virtual double ExposureHours(Block block)
    {
        var now = Api.World.Calendar.TotalHours;
        var entries = history.Where(h => h.CropCode == block.CodeWithoutParts(1));

        return entries.Sum(e =>
        {
            double start = e.StartTimeHours;
            double end = e.EndTimeHours ?? now;
            return end - start;
        });
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        InitParticles();

        if (api is ICoreServerAPI && api.World.Config.GetBool("processCrops", defaultValue: true))
        {
            FarmlandEntity.RegisterGameTickListener(ServerTick, 4500 + api.World.Rand.Next(1000));
        }
        if (api is ICoreClientAPI)
        {
            FarmlandEntity.RegisterGameTickListener(ClientTick, 400 + api.World.Rand.Next(200));
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        
        var spores = Math.Round(sporeLevel);
        if (0 < spores) dsc.AppendLine(Lang.Get("Blight spores: {0}%", spores));

        var behavior = Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(Pos.UpCopy())?.GetBehavior<BEBehaviorCropBlight>();
        if (behavior == null) return;

        var blightLevel = Math.Round(behavior.BlightLevel);
        if (0 < blightLevel) 
        {
            dsc.AppendLine(Lang.Get("Blight: {0}%", blightLevel));
        }
        else 
        {
            var chance = Math.Round(behavior.OutbreakChance() * 100);
            if (0 < chance) dsc.AppendLine(Lang.Get("Blight risk: {0}%", chance));
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("history", JsonConvert.SerializeObject(history));
        tree.SetDouble("sporeLevel", sporeLevel);
        tree.SetDouble("lastCheckTotalHours", lastCheckTotalHours);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        history = JsonConvert.DeserializeObject<List<CropHistoryEntry>>(tree.GetString("history") ?? "[]");
        sporeLevel = tree.GetDouble("sporeLevel");
        lastCheckTotalHours = tree.GetDouble("lastCheckTotalHours");
    }

    protected virtual void ServerTick(float df)
    {
        CheckHistory();
        ReduceSpores();
    }

    protected virtual void ReduceSpores()
    {
        if (SporeLevel < 0.5)
        {
            SporeLevel = 0;
            return;
        }
        const double decayRate = 0.0513; // 5.13% per day, ~100 days from 100 -> 0.5
        var now = Api.World.Calendar.TotalHours;
        if (lastCheckTotalHours == 0)
        {
            lastCheckTotalHours = now;
            return;
        }

        var deltaDays = (now - lastCheckTotalHours) / Api.World.Calendar.HoursPerDay;
        SporeLevel *= Math.Pow(1 - decayRate, deltaDays);
        lastCheckTotalHours = now;
    }

    protected virtual void ClientTick(float df)
    {
        if (sporeLevel > 0 && (CropEntity?.GetBehavior<BEBehaviorCropBlight>()?.BlightLevel ?? 0) == 0)
        {
            blightParticles.MinPos = Blockentity.Pos.ToVec3d();
            blightParticles.AddPos.Set(1, 0.0, 1); // spread around
            blightParticles.MinQuantity = (float) Math.Ceiling(sporeLevel / 20);
            blightParticles.AddQuantity = (float) Math.Ceiling(sporeLevel / 50);

            Api.World.SpawnParticles(blightParticles);
        }
    }

    protected virtual void CheckHistory()
    {
        var changed = PruneHistory();
        changed |= CheckAppendHistory();

        if (changed)
        {
            FarmlandEntity.MarkDirty(true);
            CropEntity?.MarkDirty(true);
        }
    }

    protected virtual bool PruneHistory()
    {
        var now = Api.World.Calendar.TotalHours;
        var threeYears = 3 * Api.World.Calendar.DaysPerYear * Api.World.Calendar.HourOfDay;
        var threeYearsAgo = now - threeYears;
        for (var i = 0; i < history.Count; i++)
        {
            var h = history[i];
            if (h.EndTimeHours != null && h.EndTimeHours > threeYearsAgo)
            {
                if (i > 0) 
                {
                    history = history.GetRange(i, history.Count - i);
                    return true;
                }
                return false;
            }
        }
        return false;
    }

    protected virtual bool CheckAppendHistory()
    {
        var now = Api.World.Calendar.TotalHours;

        // Guard: no crop and no history yet — nothing to do
        if (history.Count == 0 && CropEntity?.Block == null) return false;

        var latest = history.LastOrDefault();
        var current = CropEntity?.Block;

        if (current == null)
        {
            // No crop currently planted
            if (latest != null && latest.EndTimeHours == null)
            {
                latest.EndTimeHours = now;
                return true;
            }
            return false;
        }

        var currentCode = current.CodeWithoutParts(1);

        if (latest != null && latest.EndTimeHours == null && latest.CropCode == currentCode)
        {
            // Still same crop and still active — no update needed
            return false;
        }

        // Close out previous entry if needed
        if (latest != null && latest.EndTimeHours == null)
        {
            latest.EndTimeHours = now;
        }

        // Add new entry
        history.Add(new CropHistoryEntry
        {
            CropCode = currentCode,
            StartTimeHours = now,
            EndTimeHours = null
        });

        return true;
    }

    private void InitParticles()
    {
        if (Api is not ICoreClientAPI) return;
        var pos = Pos.ToVec3d();
        blightParticles = new SimpleParticleProperties(
            1f, 1f,
            ColorUtil.ToRgba(90, 120, 40, 20),
            new Vec3d(), new Vec3d(), // real position set per tick
            new Vec3f(-0.03f, 0.05f, -0.03f),
            new Vec3f(0.03f, 1f, 0.03f),
            1.5f, 0.01f, 0.1f, 0.25f
        )
        {
            MinSize = 0.3f,
            MaxSize = 0.5f,
            GravityEffect = 0.01f,
            WindAffected = true,
            WindAffectednes = 0.6f,
            ShouldDieInAir = false,
            ShouldDieInLiquid = true,
            SelfPropelled = false,
            SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.05f),
            OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.2f),
            ParticleModel = EnumParticleModel.Cube
        };
    }

    public class CropHistoryEntry
    {
        public string CropCode { get; set; }
        public double StartTimeHours { get; set; }
        public double? EndTimeHours { get; set; }
    }
}