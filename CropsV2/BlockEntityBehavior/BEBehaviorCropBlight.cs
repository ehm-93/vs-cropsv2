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

class BEBehaviorCropBlight : BlockEntityBehavior
{
    protected double blightLevel = 0;
    protected double lastCheckTotalHours = 0;
    protected MeshData mesh;
    protected Dictionary<int, Dictionary<string, string>> texturePrefixByStage;
    protected SimpleParticleProperties blightParticles;
    protected (double Weight, IPressureProvider Pressure)[] inoculumPressure;
    protected (double Weight, IPressureProvider Pressure)[] susceptibilityPressure;

    public double BlightLevel
    {
        get { return blightLevel; }
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (blightLevel == clamped) return;
            blightLevel = clamped;
            GenMesh();
            CropEntity.MarkDirty(redrawOnClient: true);
            FarmlandEntity.MarkDirty(redrawOnClient: true);
        }
    }

    public int BlightTier => (int)Math.Clamp(Math.Ceiling(BlightLevel / 20), 0, 5); // 0–5 tier scale

    public BlockEntityFarmland FarmlandEntity => Api.World.BlockAccessor
        .GetBlockEntity(Pos.DownCopy()) as BlockEntityFarmland;

    public BlockEntityCropV2 CropEntity => (BlockEntityCropV2)Blockentity;

    public BEBehaviorCropBlight(BlockEntity blockentity) : base(blockentity)
    {
        if (blockentity is not BlockEntityCropV2)
            throw new ArgumentException("Configuration error! CropBlight behavior may only be used on crops.");
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        InitParticles();
        InitPressureProviders();

        if (api is ICoreServerAPI && api.World.Config.GetBool("processCrops", defaultValue: true))
        {
            CropEntity.RegisterGameTickListener(ServerTick, 3900 + api.World.Rand.Next(200));
        }
        if (api is ICoreClientAPI)
        {
            CropEntity.RegisterGameTickListener(ClientTick, 400 + api.World.Rand.Next(200));
        }

        texturePrefixByStage = properties["texturePrefixByStage"]
            ?.AsObject<Dictionary<string, Dictionary<string, string>>>()
            ?.ToDictionary(kvp => int.Parse(kvp.Key), kvp => kvp.Value);
  
        GenMesh();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("blightLevel", blightLevel);
        tree.SetDouble("lastCheckTotalHours", lastCheckTotalHours);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        blightLevel = tree.TryGetDouble("blightLevel") ?? 0;
        lastCheckTotalHours = tree.TryGetDouble("lastCheckTotalHours") ?? 0;
        GenMesh();
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        var rounded = Math.Round(blightLevel);
        if (rounded > 0) dsc.AppendLine(Lang.Get("Blight: {0}%", rounded));
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        // copy on read to avoid concurrent mutation
        var currentMesh = mesh;
        if (currentMesh == null) return false;
        mesher.AddMeshData(currentMesh);
        return true;
    }

    public virtual void OnExchange()
    {
        GenMesh();
    }

    public virtual double OutbreakChance()
    {
        var inoculumWeight = inoculumPressure.Select(i => i.Weight).Sum();
        var inoculumRisk = inoculumPressure.Sum(i => i.Weight * i.Pressure.Value) / inoculumWeight;

        var susceptibilityRisk = susceptibilityPressure.Select(i => Math.Pow(i.Pressure.Value, i.Weight)).Aggregate((a, b) => a * b);

        var totalRisk = Math.Pow(inoculumRisk, 0.9) * Math.Pow(susceptibilityRisk, 0.1);

        var coef = InGreenhouse() ? 2 : 1;

        return totalRisk == 0 ? 0 : FunctionUtils.Sigmoid(coef * totalRisk, 0.33, 12);
    }

    protected virtual void GenMesh()
    {
        if (Api is not ICoreClientAPI capi) return;

        if (BlightTier == 0)
        {
            mesh = null;
            return;
        }

        // Get the block this crop currently represents
        Block block = CropEntity.Block;
        if (block == null || block.Shape == null) return;

        // Load the shape asset dynamically
        var shapeAsset = capi.Assets.TryGet(block.Shape.Base)?.ToObject<Shape>();
        if (shapeAsset == null)
            shapeAsset = capi.Assets.TryGet(new AssetLocation($"{block.Shape.Base.Domain}:shapes/{block.Shape.Base.Path}.json"))?.ToObject<Shape>();
        if (shapeAsset == null) return;

        // Clone and strip texture assignments (they come from texSource)
        var shape = shapeAsset.Clone();

        var texLocations = InferTextureLocations(shape);

        shape.Textures = null;

        ITexPositionSource texSource = BlightTextureSource(capi, texLocations);

        capi.Tesselator.TesselateShape(
            new TesselationMetaData
            {
                TexSource = texSource,
                TypeForLogging = "blighted crop",
                ClimateColorMapId = 1,
                SeasonColorMapId = 1
            },
            shape,
            out mesh
        );
    }

    protected virtual ITexPositionSource BlightTextureSource(ICoreClientAPI capi, Dictionary<string, AssetLocation> texLocations)
    {
        var texMap = new Dictionary<string, TextureAtlasPosition>();

        foreach (var pair in texLocations)
        {
            capi.BlockTextureAtlas.GetOrInsertTexture(pair.Value, out _, out TextureAtlasPosition texPos);
            texMap[pair.Key] = texPos;
        }

        return new DictTexSource(texMap, capi.BlockTextureAtlas.Size);
    }

    protected virtual void ServerTick(float df)
    {
        CheckBlight();
    }

    protected virtual void ClientTick(float df)
    {
        if (blightLevel > 0)
        {
            blightParticles.MinPos = Blockentity.Pos.ToVec3d();
            blightParticles.AddPos.Set(1, 0.0, 1); // spread around
            blightParticles.MinQuantity = (float) Math.Ceiling(blightLevel / 20);
            blightParticles.AddQuantity = (float) Math.Ceiling(blightLevel / 50);

            Api.World.SpawnParticles(blightParticles);
        }
    }

    protected virtual void CheckBlight()
    {
        if (lastCheckTotalHours == 0)
        {
            lastCheckTotalHours = Api.World.Calendar.TotalHours;
            return;
        }

        var now = Api.World.Calendar.TotalHours;
        var deltaDays = (now - lastCheckTotalHours) / Api.World.Calendar.HoursPerDay;
        lastCheckTotalHours = Api.World.Calendar.TotalHours;
        
        var chance =  1 - Math.Pow(1 - OutbreakChance(), deltaDays);
        var roll = Api.World.Rand.NextDouble();
        if (roll < chance)
        {
            BlightLevel += 10 + BlightLevel / 2;
        }
    }

    protected virtual void InitPressureProviders()
    {
        inoculumPressure = new (double, IPressureProvider)[]
        {
            (0.4, new NeighborPressureProvider(Api, Pos)),
            (0.4, new HistoryPressureProvider(this)),
            (0.2, new SporePressureProvider()),
        };
        susceptibilityPressure = new (double, IPressureProvider)[]
        {
            (0.15, new TemperaturePressureProvider(Api.World, Pos)),
            (0.15, new MoisturePressureProvider(FarmlandEntity)),
            (0.05, new MulchPresureProvider(Api, Pos)),
            (0.6, new GenerationPressureProvider(CropEntity)),
            (0.05, new WeedPressureProvider(Api, Pos)),
        };
    }

    private bool InGreenhouse()
    {
        return FarmlandEntity.roomness > 0;
    }

    private Dictionary<string, AssetLocation> InferTextureLocations(Shape shape)
    {
        var texLocations = new Dictionary<string, AssetLocation>();

        // Step 1: Pull all texture codes used in shape
        if (shape.Textures != null)
        {
            foreach (var pair in shape.Textures)
            {
                texLocations[pair.Key] = new AssetLocation($"cropsv2:{pair.Value.Path}-blight{BlightTier}");
            }
        }

        // Step 2: Allow override from patch file
        if (texturePrefixByStage != null && texturePrefixByStage.TryGetValue(CropStage(), out var texturePrefix))
        {
            foreach (var pair in texturePrefix)
            {
                texLocations[pair.Key] = new AssetLocation($"{pair.Value}-blight{BlightTier}");
            }
        }

        return texLocations;
    }

    private int CropStage()
    {
        if (CropEntity?.Block is not BlockCrop crop) return 1;
        if (int.TryParse(crop.LastCodePart(), out var result)) return result;
        return 1;
    }

    private void InitParticles()
    {
        if (Api is not ICoreClientAPI capi) return;
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

    protected interface IPressureProvider
    {
        public double Value { get; }
    }

    private class HistoryPressureProvider : IPressureProvider
    {
        private const double MaxExposureYears = 3.0;
        private const double SigmoidDeadZone = 0.33;
        private const double SigmoidMidpoint = 0.66;
        private const double SigmoidSharpness = 16;

        private readonly BEBehaviorCropBlight blight;

        private static readonly System.Func<double, double> PressureCurve = FunctionUtils.MemoizeStepBounded(
            0.01, 0, 1,
            x => x < SigmoidDeadZone ? 0 : FunctionUtils.Sigmoid(x, SigmoidMidpoint, SigmoidSharpness)
        );

        public HistoryPressureProvider(BEBehaviorCropBlight cropBlight)
        {
            this.blight = cropBlight;
        }

        public double Value
        {
            get
            {
                var farmland = blight.FarmlandEntity;
                if (farmland == null) return 0;

                var behavior = farmland.GetBehavior<BEBehaviorFarmlandBlight>();
                if (behavior == null) return 0;

                var crop = blight.CropEntity?.Block;
                if (crop == null) return 0;

                double exposure = behavior.ExposureHours(crop);
                double maxExposure = MaxExposureYears * blight.Api.World.Calendar.DaysPerYear * blight.Api.World.Calendar.HoursPerDay;

                double normalized = GameMath.Clamp(exposure / maxExposure, 0, 1);

                return PressureCurve(normalized);
            }
        }
    }

    private class NeighborPressureProvider : IPressureProvider
    {
        // Sigmoid: center at 0.5 (50%), steepness tuned for ramping between 0.25–0.50
        private const double a = 16;  // steepness
        private const double b = 0.08; // midpoint
        private readonly ICoreAPI Api;
        private readonly IEnumerable<BlockPos> neighborPositions;
        private readonly Func<double> Blightness;
        
        private readonly static System.Func<double, double> CalculatePressure = FunctionUtils.MemoizeStepBounded(0.01, 0, 1, x =>
            x == 0 ? 0 : FunctionUtils.Sigmoid(x, b, a)
        );

        public NeighborPressureProvider(ICoreAPI Api, BlockPos Pos, int d = 2)
        {
            this.Api = Api;

            // Build a 2D square of positions around the crop
            var offsets = new List<BlockPos>();
            for (int dx = -1 * d; dx <= d; dx++)
            {
                for (int dz = -1 * d; dz <= d; dz++)
                {
                    if (dx == 0 && dz == 0) continue; // Skip self
                    offsets.Add(Pos.AddCopy(dx, 0, dz));
                }
            }

            neighborPositions = offsets;

            Blightness = FunctionUtils.MemoizeFor(TimeSpan.FromSeconds(30),
                () =>
                {
                    double totalBlight = 0;
                    double totalWeight = 0;

                    foreach (var pos in neighborPositions)
                    {
                        double dist = pos.DistanceTo(Pos); // Euclidean distance
                        double weight = 1 / Math.Max(dist, 1); // Avoid division by 0
                        totalBlight += GetBlightLevel(pos) * weight;
                        totalWeight += weight;
                    }

                    return totalBlight / (totalWeight * 100);
                }
            );
        }

        public double Value
        {
            get
            {
                return CalculatePressure(Blightness());
            }
        }

        private double GetBlightLevel(BlockPos pos)
        {
            if (Api.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCropV2 entity) return 0;

            var weeds = entity.GetBehavior<BEBehaviorCropBlight>();
            if (weeds == null) return 0;

            return weeds.BlightLevel;
        }
    }

    private class SporePressureProvider : IPressureProvider
    {
        // todo
        public double Value => 0;
    }

    private class WeedPressureProvider : IPressureProvider
    {
        private readonly ICoreAPI Api;
        private readonly BlockPos Pos;

        // Tunables
        private const double maxWeedLevel = 100.0;
        private const double midpoint = 0.5;   // 50% weed level
        private const double steepness = 8;    // Sigmoid steepness

        private static readonly System.Func<double, double> WeedToPressure = FunctionUtils.MemoizeStepBounded(
            1, 0, maxWeedLevel,
            x => FunctionUtils.Sigmoid(x / maxWeedLevel, midpoint, steepness)
        );

        public WeedPressureProvider(ICoreAPI api, BlockPos pos)
        {
            Api = api;
            Pos = pos;
        }

        public double Value
        {
            get
            {
                var be = Api.World.BlockAccessor.GetBlockEntity(Pos) as BlockEntityCropV2;
                var weedBehavior = be?.GetBehavior<BEBehaviorCropWeeds>();
                if (weedBehavior == null) return 0;

                return WeedToPressure(weedBehavior.WeedLevel);
            }
        }
    }


    private class MoisturePressureProvider : IPressureProvider
    {
        private readonly BlockEntityFarmland farmland;

        public MoisturePressureProvider(BlockEntityFarmland farmland)
        {
            this.farmland = farmland;
        }

        public double Value
        {
            get
            {
                double moisture = farmland.MoistureLevel;
                double ideal = 0.4; // Ideal midpoint
                double range = 0.3; // Spread around the midpoint
                double deviation = (moisture - ideal) / range;

                // U-shaped curve: 0 at ideal, 1 at extremes
                return Math.Clamp(deviation * deviation, 0, 1);
            }
        }
    }

    private class TemperaturePressureProvider : IPressureProvider
    {
        private readonly IWorldAccessor world;
        private readonly BlockPos pos;

        public TemperaturePressureProvider(IWorldAccessor world, BlockPos pos)
        {
            this.world = world;
            this.pos = pos;
        }

        public double Value
        {
            get
            {
                double temp = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues).Temperature;

                // Ideal blight temperature = 20°C ± 5°C
                double ideal = 25.0;
                double spread = 10.0;

                // Normalize deviation from ideal
                double deviation = (temp - ideal) / spread;

                // Gaussian-shaped curve: max at ideal, drops off with distance
                double pressure = Math.Exp(-deviation * deviation);

                return Math.Clamp(pressure, 0, 1);
            }
        }
    }

    private class MulchPresureProvider : IPressureProvider
    {
        private readonly ICoreAPI Api;
        private readonly BlockPos Pos;

        public MulchPresureProvider(ICoreAPI api, BlockPos pos)
        {
            Api = api;
            Pos = pos;
        }

        public double Value
        {
            get
            {
                var blockEntity = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy()) as BlockEntityFarmland;
                if (blockEntity == null) return 0;

                var mulchBehavior = blockEntity.GetBehavior<BEBehaviorFarmlandMulch>();
                if (mulchBehavior == null) return 0;

                double mulchLevel = Math.Max(0.1, mulchBehavior.MulchLevel);

                // Normalize to 0–1, apply quadratic ramp
                double norm = Math.Clamp(mulchLevel / 100.0, 0, 1);
                return norm * norm * 0.35; // Max pressure = 0.35
            }
        }
    }

    private class GenerationPressureProvider : IPressureProvider
    {
        private readonly BlockEntityCropV2 cropEntity;

        private static readonly System.Func<double, double> PressureCurve = 
            FunctionUtils.MemoizeStepBounded(
                step: 0.05,
                minInclusive: 0,
                maxInclusive: 1,
                fn: x => FunctionUtils.Sigmoid(x, 0.6, 12)
            );

        public GenerationPressureProvider(BlockEntityCropV2 cropEntity)
        {
            this.cropEntity = cropEntity;
        }

        public double Value
        {
            get
            {
                int generation = cropEntity?.Generation ?? 0;

                if (generation <= 0)
                {
                    return 0; // Wild or uncultivated crop
                }

                // Normalize gen 1–10 → 0–1
                double norm = Math.Clamp(generation / 10.0, 0, 1);

                return PressureCurve(norm);
            }
        }
    }
}
