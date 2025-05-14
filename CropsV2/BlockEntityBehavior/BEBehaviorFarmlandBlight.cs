using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BEBehaviorFarmlandBlight : BlockEntityBehavior
{
    protected List<CropHistoryEntry> history = new();
    public BlockEntityFarmland FarmlandEntity => Blockentity as BlockEntityFarmland;

    public BlockEntityCropV2 CropEntity =>
        Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(Pos.UpCopy());

    public BEBehaviorFarmlandBlight(BlockEntity blockEntity) : base(blockEntity)
    {
        if (blockEntity is not BlockEntityFarmland) throw new ArgumentException("Configuration error! FarmlandBlight behavior may only be assigned to farmland.");
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

        if (api is ICoreServerAPI && api.World.Config.GetBool("processCrops", defaultValue: true))
        {
            FarmlandEntity.RegisterGameTickListener(ServerTick, 45_000 + api.World.Rand.Next(30_000));
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        var crop = Api.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(Pos.UpCopy());
        if (crop == null) return;

        var behavior = crop.GetBehavior<BEBehaviorCropBlight>();
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
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        history = JsonConvert.DeserializeObject<List<CropHistoryEntry>>(tree.GetString("history") ?? "[]");
    }

    protected virtual void ServerTick(float df)
    {
        CheckHistory();
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

    public class CropHistoryEntry
    {
        public string CropCode { get; set; }
        public double StartTimeHours { get; set; }
        public double? EndTimeHours { get; set; }
    }
}