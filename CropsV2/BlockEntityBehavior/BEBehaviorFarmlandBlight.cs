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

class BEBehaviorFarmlandBlight : BlockEntityBehavior, IOnBlockInteract
{
    public BlockEntityFarmland FarmlandEntity => Blockentity as BlockEntityFarmland;
    protected List<CropHistoryEntry> history = new();
    protected double sporeLevel = 0;
    protected double sporeTreatment = 0;
    protected double lastCheckTotalHours = 0;
    protected SimpleParticleProperties blightParticles;
    private bool blightEnabled = true;
    private bool sporesEnabled = true;

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

    public double SporeTreatment
    {
        get { return sporeTreatment; }
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (clamped != sporeTreatment)
            {
                sporeTreatment = clamped;
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

        blightEnabled = WorldConfig.EnableBlight;
        sporesEnabled = WorldConfig.EnableSpores;

        InitParticles();

        if ((blightEnabled || sporesEnabled) && api is ICoreServerAPI && api.World.Config.GetBool("processCrops", defaultValue: true))
        {
            FarmlandEntity.RegisterGameTickListener(ServerTick, 4500 + api.World.Rand.Next(1000));
        }
        if (sporesEnabled && api is ICoreClientAPI)
        {
            FarmlandEntity.RegisterGameTickListener(ClientTick, 400 + api.World.Rand.Next(200));
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        if (sporesEnabled)
        {
            var spores = Math.Round(sporeLevel);
            if (0 < spores) dsc.AppendLine(Lang.Get("Blight spores: {0}%", spores));

            var treatment = Math.Round(SporeTreatment);
            if (0 < treatment) dsc.AppendLine(Lang.Get("Spore treatment: {0}%", treatment));
        }

        if (blightEnabled)
        {
            var behavior = Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(Pos.UpCopy())?.GetBehavior<BEBehaviorCropBlight>();
            if (behavior != null)
            {
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
        }
    }

    public bool OnBlockInteract(IPlayer byPlayer)
    {
        if (!sporesEnabled) return false;

        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot?.Itemstack?.Item?.Code?.Path != "lime") return false;
        if (sporeTreatment == 100) return false;

        SporeTreatment += 50;

        if (!byPlayer.WorldData.CurrentGameMode.HasFlag(EnumGameMode.Creative))
        {
            slot.TakeOut(1);
            slot.MarkDirty();
        }

        Api.World.PlaySoundAt(
            Api.World.BlockAccessor.GetBlock(Pos).Sounds.Hit,
            Pos.X + 0.5,
            Pos.InternalY + 0.75,
            Pos.Z + 0.5,
            byPlayer, 
            randomizePitch: true,
            12f
        );

        return true;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("history", JsonConvert.SerializeObject(history));
        tree.SetDouble("sporeLevel", sporeLevel);
        tree.SetDouble("lastCheckTotalHours", lastCheckTotalHours);
        tree.SetDouble("sporeTreatment", sporeTreatment);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        history = JsonConvert.DeserializeObject<List<CropHistoryEntry>>(tree.GetString("history") ?? "[]");
        sporeLevel = tree.GetDouble("sporeLevel");
        lastCheckTotalHours = tree.GetDouble("lastCheckTotalHours");
        sporeTreatment = tree.GetDouble("sporeTreatment");
    }

    protected virtual void ServerTick(float df)
    {
        CheckHistory();
        ReduceSpores();
    }

    protected virtual void ReduceSpores()
    {
        if (!sporesEnabled) return;

        var now = Api.World.Calendar.TotalHours;
        if (lastCheckTotalHours == 0)
        {
            lastCheckTotalHours = now;
            return;
        }

        if (sporeTreatment > 0) TreatSpores();
        else DecaySpores(0.02);

        lastCheckTotalHours = now;
    }

    protected virtual void TreatSpores()
    {
        var normalized = sporeTreatment / 100;
        DecaySpores(Math.Max(normalized * 0.2, 0.04));

        var now = Api.World.Calendar.TotalHours;
        var deltaDays = (now - lastCheckTotalHours) / Api.World.Calendar.HoursPerDay;

        if (SporeTreatment < 0.5) SporeTreatment = 0;
        else SporeTreatment *= Math.Pow(1 - 0.05, deltaDays);
        
        FarmlandEntity.Nutrients[0] *= (float) Math.Pow(1 - 0.1, deltaDays);
        FarmlandEntity.Nutrients[1] *= (float) Math.Pow(1 - 0.1, deltaDays);
        FarmlandEntity.Nutrients[2] *= (float) Math.Pow(1 - 0.1, deltaDays);
    }

    protected virtual void DecaySpores(double decayRate)
    {
        var now = Api.World.Calendar.TotalHours;
        var deltaDays = (now - lastCheckTotalHours) / Api.World.Calendar.HoursPerDay;
        if (SporeLevel < 0.5) SporeLevel = 0;
        else SporeLevel *= Math.Pow(1 - decayRate, deltaDays);
    }

    protected virtual void ClientTick(float df)
    {
        if (sporeLevel > 0 && (CropEntity?.GetBehavior<BEBehaviorCropBlight>()?.BlightLevel ?? 0) == 0)
        {
            blightParticles.MinPos = Blockentity.Pos.ToVec3d();
            blightParticles.AddPos.Set(1, 0.0, 1); // spread around
            blightParticles.MinQuantity = (float)Math.Ceiling(sporeLevel / 20);
            blightParticles.AddQuantity = (float)Math.Ceiling(sporeLevel / 50);

            Api.World.SpawnParticles(blightParticles);
        }
    }

    protected virtual void CheckHistory()
    {
        if (!blightEnabled) return;

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