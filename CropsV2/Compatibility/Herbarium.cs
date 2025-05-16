using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ehm93.VintageStory.CropsV2;

class Herbarium
{
    public static bool IsLoaded()
    {
        if (Type.GetType("herbarium.Herbarium, Herbarium") == null) return false;
        else return true;
    }

    public static Type BEBerryPlantType()
    {
        return Type.GetType("herbarium.BEBerryPlant, Herbarium");
    }

    public static Type BEGroundBerryPlantType()
    {
        return Type.GetType("herbarium.BEGroundBerryPlant, Herbarium");
    }

    public static Type BEHerbariumBerryBushType()
    {
        return Type.GetType("herbarium.BEHerbariumBerryBush, Herbarium");
    }

    public class BEBehaviorBerryChilling : BEBehaviorBerryPlant, HasChill, OnExchanged
    {
        private readonly CropsV2.BEBehaviorBerryChilling self;
        public bool Chilling
        {
            get { return self.Chilling; }
            set { self.Chilling = value; }
        }
        public double ChillProgress => self.ChillProgress;

        public BEBehaviorBerryChilling(BlockEntity blockentity) : base(blockentity)
        {
            self = new CropsV2.BEBehaviorBerryChilling(blockentity);
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            self.Initialize(api, properties);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            self.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            self.OnBlockUnloaded();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            self.OnBlockBroken(byPlayer);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            self.OnBlockPlaced(byItemStack);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            self.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            self.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);
            self.OnReceivedClientPacket(fromPlayer, packetid, data);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            self.OnReceivedServerPacket(packetid, data);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            self.GetBlockInfo(forPlayer, dsc);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            self.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
            self.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            self.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return base.OnTesselation(mesher, tessThreadTesselator) || self.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
        {
            base.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);
            self.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);
        }

        public void OnExchanged(Block block)
        {
            self.OnExchanged(block);
        }
    }
}