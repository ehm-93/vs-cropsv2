using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class BlockEntityFarmlandV2 : BlockEntityFarmland
{
    protected volatile MeshData mulchQuad;
    protected TextureAtlasPosition mulchTexturePos;
    private int _mulchLevel = 0;

    public int MulchLevel {
        get => _mulchLevel;
        protected set 
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (_mulchLevel != clamped)
            {
                _mulchLevel = clamped;
                if (GenMulchQuad()) MarkDirty(redrawOnClient: true);
            }
        }
    }

    public MulchTierEnum MulchTier {
        get
        {
            if (_mulchLevel == 0) return MulchTierEnum.None;
            if (_mulchLevel <= 33) return MulchTierEnum.Low;
            if (_mulchLevel <= 66) return MulchTierEnum.Medium;
            return MulchTierEnum.High;
        }
     }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (GenMulchQuad()) MarkDirty(redrawOnClient: true);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("_mulchLevel", _mulchLevel);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        MulchLevel = tree.TryGetInt("_mulchLevel") ?? 0;
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (mulchQuad != null) mesher.AddMeshData(mulchQuad);
        return base.OnTesselation(mesher, tesselator);
    }

    public virtual bool OnBlockInteractV2(IPlayer byPlayer)
    {
        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot?.Itemstack == null) return false;

        if (slot.Itemstack.Collectible.Code.Path == "drygrass") 
        {
            return OnBlockInteractWithDryGrass(byPlayer, slot);
        }

        return base.OnBlockInteract(byPlayer);
    }

    protected virtual bool OnBlockInteractWithDryGrass(IPlayer byPlayer, ItemSlot slot)
    {
        if (MulchLevel >= 100) return false;

        MulchLevel += 33;
        if (!byPlayer.WorldData.CurrentGameMode.HasFlag(EnumGameMode.Creative))
        {
            slot.TakeOut(1);
            slot.MarkDirty();
        }

        Api.World.PlaySoundAt(
            new AssetLocation("game:sounds/block/grass1"),
            Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5,
            byPlayer,
            randomizePitch: true,
            range: 8
        );

        MarkDirty();
        return true;
    }

    protected virtual AssetLocation MulchTextureLocation()
    {
        string mulchTexture = MulchTier switch
        {
            MulchTierEnum.Low => "low",
            MulchTierEnum.Medium => "med",
            MulchTierEnum.High => "high",
            _ => "low"
        };
        return new AssetLocation($"cropsv2:block/soil/farmland/mulch-{mulchTexture}");
    }

    protected virtual AssetLocation MulchShapeLocation()
    {
        return new AssetLocation("cropsv2:shapes/block/soil/farmland/mulch.json");
    }

    private bool GenMulchQuad()
    {
        if (Api is not ICoreClientAPI capi) return false;

        if (MulchTier == MulchTierEnum.None)
        {
            if (mulchQuad != null)
            {
                mulchQuad = null;
                return true;
            }
            else
            {
                return false;
            }
        }

        Shape shape = capi.Assets.Get(MulchShapeLocation()).ToObject<Shape>();

        capi.BlockTextureAtlas.GetOrInsertTexture(MulchTextureLocation(), out _, out mulchTexturePos);

        var texMap = new Dictionary<string, TextureAtlasPosition> {
            { "side", mulchTexturePos }
        };

        var texSource = new DictTexSource(texMap, capi.BlockTextureAtlas.Size);

        capi.Tesselator.TesselateShape(
            new TesselationMetaData {
                TypeForLogging = "farmland mulch quad",
                TexSource = texSource
            },
            shape,
            out mulchQuad
        );

        return true;
    }

    public enum MulchTierEnum
    {
        None, Low, Medium, High
    }

    [HarmonyPatchCategory("cropsv2")]
    [HarmonyPatch(typeof(BlockEntityFarmland), "updateMoistureLevel", new Type[] {
        typeof(double), typeof(float), typeof(bool), typeof(ClimateCondition)
    })]
    internal static class UpdateMoistureLevelPatch
    {
        [HarmonyPrefix]
        public static void Before(BlockEntityFarmland __instance, ref float __state)
        {
            if (__instance is not BlockEntityFarmlandV2 self) return;

            // remmber moisture level before mutated by base class
            __state = self.moistureLevel;
        }

        [HarmonyPostfix]
        public static void After(BlockEntityFarmland __instance, ref float __state)
        {
            if (__instance is not BlockEntityFarmlandV2 self) return;
            
            // slow down moisture loss depending on mulch level
            var diff = __state - self.moistureLevel;
            var mulchCoef = 0.0075f * self.MulchLevel;
            if (diff > 0) self.moistureLevel += mulchCoef * diff;
        }
    }

    private class DictTexSource : ITexPositionSource
    {
        private readonly Dictionary<string, TextureAtlasPosition> texMap;
        private readonly Size2i atlasSize;

        public DictTexSource(Dictionary<string, TextureAtlasPosition> texMap, Size2i atlasSize)
        {
            this.texMap = texMap;
            this.atlasSize = atlasSize;
        }

        public Size2i AtlasSize => atlasSize;

        public TextureAtlasPosition this[string textureCode] =>
                texMap.TryGetValue(textureCode, out var pos) ? pos : null;
    }
}
