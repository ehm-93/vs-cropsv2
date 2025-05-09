using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

// TODO: weeds slow crop growth, later generations more impacted, earlier growth stages more affected

class BEBehaviorCropWeeds : BlockEntityBehavior
{
    readonly private double minSproutChance = 0.001;
    readonly private double maxSproutChance = 0.5;
    readonly private double maxGrowChance = 1;
    readonly private double minGrowChance = 0.01;
    readonly private double growth = 10;
    readonly private double neighborWeight = 4;
    readonly private double moistureWeight = 1;
    readonly private double nutritionWeight = 2;
    readonly private double cropMaturityWeight = 2;
    protected double weedLevel;
    protected double lastCheckTotalHours = 0;
    protected MeshData weedMesh;
    IEnumerable<BlockPos> neighborPositions;

    public double WeedLevel {
        get { return weedLevel; }
        set {
            var clamped = Math.Clamp(value, 0, 100);
            if (weedLevel != clamped) {
                weedLevel = clamped;
                if (!GenWeedMesh()) CropEntity.MarkDirty(redrawOnClient: true);
            }
        }
    }

    public BlockEntityFarmland FarmlandEntity {
        get {
            var entity = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy());
            if (entity is not BlockEntityFarmland farmland) return null;
            return farmland;
        }
    }

    public BlockEntityCropV2 CropEntity {
        get { return (BlockEntityCropV2) Blockentity; }
    }

    public BEBehaviorCropWeeds(BlockEntity blockEntity)
        : base(blockEntity)
    {
        if (blockEntity is not BlockEntityCropV2) throw new ArgumentException("Configuration error! CropWeeds behavior may only be used on crops.");
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        if (api is ICoreServerAPI)
        {
            if (Api.World.Config.GetBool("processCrops", defaultValue: true))
            {
                CropEntity.RegisterGameTickListener(Tick, 3900 + api.World.Rand.Next(200));
            }
        }
        
        neighborPositions = new BlockPos[]
        {
            Pos.NorthCopy(),
            Pos.NorthCopy().EastCopy(),
            Pos.EastCopy(),
            Pos.SouthCopy().EastCopy(),
            Pos.SouthCopy(),
            Pos.SouthCopy().WestCopy(),
            Pos.WestCopy(),
            Pos.NorthCopy().WestCopy(),
        };

        GenWeedMesh();
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        if (weedMesh != null) mesher.AddMeshData(weedMesh);
        return base.OnTesselation(mesher, tessThreadTesselator);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("weedLevel", weedLevel);
        tree.SetDouble("lastCheckTotalHours", lastCheckTotalHours);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        weedLevel = tree.TryGetDouble("weedLevel") ?? 0;
        lastCheckTotalHours = tree.TryGetDouble("lastCheckTotalHours") ?? 0;
        
        GenWeedMesh();
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (weedLevel > 0) dsc.AppendLine(Lang.Get("Weeds: {0}%", Math.Round(weedLevel)));
    }
    
    protected virtual bool GenWeedMesh()
    {
        if (Api is not ICoreClientAPI capi) return false;

        if (WeedLevel == 0)
        {
            if (weedMesh != null)
            {
                weedMesh = null;
                return true;
            }
            else
            {
                return false;
            }
        }

        var shape = capi.Assets.Get(WeedShapeLocation()).ToObject<Shape>();

        var texSource = WeedTextureSource(capi);

        capi.Tesselator.TesselateShape(
            new TesselationMetaData {
                TypeForLogging = "farmland weed mesh",
                TexSource = texSource,
                ClimateColorMapId = 1,
                SeasonColorMapId = 1,
            },
            shape,
            out weedMesh
        );


        var rotateY = GetJitterOffset(Pos, 0);
        weedMesh.Rotate(new Vec3f(), 0, rotateY, 0);

        var offsetX = GetJitterOffset(Pos, 1);
        var offsetZ = GetJitterOffset(Pos, 2);
        weedMesh.Translate(new Vec3f(offsetX, 0, offsetZ));

        return true;
    }

    protected virtual AssetLocation WeedShapeLocation()
    {
        return new AssetLocation("cropsv2:shapes/block/plant/weeds.json");
    }

    protected virtual ITexPositionSource WeedTextureSource(ICoreClientAPI capi)
    {
        TextureAtlasPosition northTexturePos;
        capi.BlockTextureAtlas.GetOrInsertTexture(WeedNorthTextureLocation(), out _, out northTexturePos);

        TextureAtlasPosition southTexturePos;
        capi.BlockTextureAtlas.GetOrInsertTexture(WeedSouthTextureLocation(), out _, out southTexturePos);

        var texMap = new Dictionary<string, TextureAtlasPosition> {
            { "north", northTexturePos },
            { "south", southTexturePos },
        };

        return new DictTexSource(texMap, capi.BlockTextureAtlas.Size);
    }

    protected virtual void Tick(float df)
    {
        CheckGrowWeeds();
    }

    protected virtual void CheckGrowWeeds()
    {
        if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;

        double now = Api.World.Calendar.TotalHours;

        if (HasMulch() || lastCheckTotalHours == 0)
        {
            lastCheckTotalHours = now;
            return;
        }

        double deltaDays = (now - lastCheckTotalHours) / 24.0;
        lastCheckTotalHours = now;

        double roll = Api.World.Rand.NextDouble();
        if (weedLevel == 0)
        {
            double sproutProb = 1 - Math.Pow(1 - WeedSproutChance(), deltaDays);
            if (roll < sproutProb) WeedLevel += growth;
        }
        else
        {
            double growProb = 1 - Math.Pow(1 - WeedGrowthChance(), deltaDays);
            if (roll < growProb) WeedLevel += growth;
        }
    }

    protected double WeedSproutChance()
    {
        double totalPressure = MoisturePressure() + NutritionPressure() + NeighborPressure();
        double antiPressure = CropMaturityAntiPressure();
        const double a = 1.3;
        const double b = 3.5;
        var sproutChance = 1.0 / (1.0 + Math.Exp(-a * (totalPressure - b))) / antiPressure;
        return Math.Min(1, maxSproutChance * sproutChance + minSproutChance);
    }

    protected virtual double WeedGrowthChance()
    {
        double totalPressure = MoisturePressure() + NutritionPressure();
        double antiPressure = CropMaturityAntiPressure();
        const double a = 1.0;
        const double b = 2.0;
        var growthChance = 1.0 / (1.0 + Math.Exp(-a * (totalPressure - b))) / antiPressure;
        return Math.Min(1, maxGrowChance * growthChance + minGrowChance);
    }

    private AssetLocation WeedNorthTextureLocation()
    {
        return new AssetLocation($"cropsv2:block/plant/weeds/{WeedLevelString()}-north");
    }

    private AssetLocation WeedSouthTextureLocation()
    {
        return new AssetLocation($"cropsv2:block/plant/weeds/{WeedLevelString()}-south");
    }

    private string WeedLevelString()
    {
        return WeedLevel switch {
            0 => "none",
            <20 => "veryshort",
            <40 => "short",
            <60 => "medium",
            <80 => "tall",
            _ => "verytall"
        };
    }

    private double NutritionPressure()
    {
        double nutrientSum = FarmlandEntity?.Nutrients.Sum() ?? 0;

        // Normalize to [0, 1] based on a soft cap of 200 total nutrients
        double x = GameMath.Clamp(nutrientSum / 200.0, 0, 1);

        // Gentle sigmoid centered around 120 nutrients (x = 0.6)
        const double a = 8.0;   // steepness
        const double b = 0.6;   // midpoint

        return nutritionWeight * 1.0 / (1.0 + Math.Exp(-a * (x - b)));
    }

    private double NeighborPressure()
    {
        var weediness = neighborPositions.Sum(GetWeedLevel);

        var x = Math.Clamp(weediness / 800, 0, 1);

        // Sigmoid: center at 0.5 (50%), steepness tuned for ramping between 0.25–0.50
        const double a = 12;  // steepness
        const double b = 0.33; // midpoint
        return neighborWeight * 1.0 / (1.0 + Math.Exp(-a * (x - b)));
    }

    private double MoisturePressure()
    {
        double? moisture = FarmlandEntity?.MoistureLevel;
        if (moisture == null) return 0;

        double x = Math.Clamp(moisture.Value, 0, 1);

        // Tuned so that at 15% moisture, pressure = 50%
        const double k = 4.62;

        return moistureWeight * Math.Clamp(1 - Math.Exp(-k * x), 0, 1);
    }

    private double CropMaturityAntiPressure()
    {
        var maturity = CropStage() / CropFinalStage();
        const double a = 12;  // steepness
        const double b = 0.33; // midpoint
        return Math.Max(0.5, cropMaturityWeight * 1.0 / (1.0 + Math.Exp(-a * (maturity - b))));
    }

    private bool HasMulch()
    {
        var farmland = FarmlandEntity;
        if (farmland == null) return false;

        var behavior = farmland.GetBehavior<BEBehaviorFarmlandMulch>();
        if (behavior == null) return false;

        return 0 < behavior.MulchLevel;
    }

    private double GetWeedLevel(BlockPos pos)
    {
        if (Api.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCropV2 entity) return 0;

        var weeds = entity.GetBehavior<BEBehaviorCropWeeds>();
        if (weeds == null) return 0;

        return weeds.WeedLevel;
    }

    private int CropStage()
    {
        if (CropEntity?.Block is not BlockCrop crop) return 1;
        int.TryParse(crop.LastCodePart(), out var result);
        return result;
    }

    private int CropFinalStage()
    {
        if (CropEntity?.Block is not BlockCrop crop) return 1;
        return crop.CropProps.GrowthStages;
    }

    private float GetJitterOffset(BlockPos pos, int seed)
    {
        int hash = (pos.X * 73856093) ^ (pos.Y * 19349663) ^ (pos.Z * 83492791) ^ seed;
        Random rand = new Random(hash);
        return (float) (rand.NextDouble() - 0.5f) * 0.5f;
    }
}