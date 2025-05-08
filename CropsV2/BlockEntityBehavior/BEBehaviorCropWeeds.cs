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

class BEBehaviorCropWeeds : BlockEntityBehavior
{
    readonly private Random rand = new Random();
    readonly private double baseWeedSproutChance = 0.05;
    readonly private double weedGrowChance = 0.25;
    readonly private double growth = 10;
    protected double weedLevel;
    protected double lastCheckTotalHours = 0;
    protected MeshData weedMesh;
    protected BlockPos northPos;
    protected BlockPos northEastPos;
    protected BlockPos eastPos;
    protected BlockPos southEastPos;
    protected BlockPos southPos;
    protected BlockPos southWestPos;
    protected BlockPos westPos;
    protected BlockPos northWestPos;

    public double WeedLevel {
        get { return weedLevel; }
        protected set { 
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
                CropEntity.RegisterGameTickListener(Tick, 3900 + rand.Next(200));
            }
        }
        
        northPos = Pos.NorthCopy();
        northEastPos = Pos.NorthCopy().EastCopy();
        eastPos = Pos.EastCopy();
        southEastPos = Pos.SouthCopy().EastCopy();
        southPos = Pos.SouthCopy();
        southWestPos = Pos.SouthCopy().WestCopy();
        westPos = Pos.WestCopy();
        northWestPos = Pos.NorthCopy().WestCopy();

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

    private AssetLocation WeedNorthTextureLocation()
    {
        return new AssetLocation("cropsv2:block/plant/weeds/veryshort-north");
    }

    private AssetLocation WeedSouthTextureLocation()
    {
        return new AssetLocation("cropsv2:block/plant/weeds/veryshort-south");
    }

    protected virtual void Tick(float df)
    {
        CheckGrowWeeds();
    }

    protected virtual void CheckGrowWeeds()
    {
        if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;

        double now = Api.World.Calendar.TotalHours;

        if (HasMulch(Pos) || lastCheckTotalHours == 0)
        {
            lastCheckTotalHours = now;
            return;
        }

        double deltaDays = (now - lastCheckTotalHours) / 24.0;
        lastCheckTotalHours = now;

        // Bernoulli-style scaled probability
        double sproutProb = 1 - Math.Pow(1 - WeedSproutChance(), deltaDays);
        double growProb = 1 - Math.Pow(1 - weedGrowChance, deltaDays);

        double roll = rand.NextDouble();

        if (weedLevel == 0)
        {
            if (roll < sproutProb) WeedLevel += growth;
        }
        else
        {
            if (roll < growProb) WeedLevel += growth;
        }
    }

    protected virtual double WeedSproutChance()
    {
        var weedy = new List<bool>
        {
            HasWeeds(northPos),
            HasWeeds(northEastPos),
            HasWeeds(eastPos),
            HasWeeds(southEastPos),
            HasWeeds(southPos),
            HasWeeds(southWestPos),
            HasWeeds(westPos),
            HasWeeds(northWestPos),
        };

        int count = weedy.Sum(i => i ? 1 : 0);
        return NutritionCoef() * (1 - Math.Pow(1 - baseWeedSproutChance, count) + 0.001);
    }

    private double NutritionCoef()
    {
        return FarmlandEntity.Nutrients.Sum() switch
        {
            < 40 => 0.25,
            < 65 => 0.5,
            < 100 => 0.75,
            < 150 => 1,
            _ => 2,
        };
    }

    private bool HasWeeds(BlockPos pos)
    {
        var entity = Api.World.BlockAccessor.GetBlockEntity(pos);
        if (entity is not BlockEntityCropV2 crop) return false;
        
        var behavior = crop.GetBehavior<BEBehaviorCropWeeds>();
        if (behavior == null) return false;

        return 0 < behavior.WeedLevel;
    }

    private bool HasMulch(BlockPos pos)
    {
        var farmland = FarmlandEntity;
        if (farmland == null) return false;

        var behavior = farmland.GetBehavior<BEBehaviorFarmlandMulch>();
        if (behavior == null) return false;

        return 0 < behavior.MulchLevel;
    }
}