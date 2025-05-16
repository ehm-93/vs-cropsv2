using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Ehm93.VintageStory.CropsV2;

public class BlockEntityCropV2 : BlockEntity {
    private int generation = 0;

    public int Generation { 
        get { return generation; }
        set {
            generation = value;
            MarkDirty();
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("generation", generation);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        generation = tree.GetInt("generation", 0);
    }

    public override void OnExchanged(Block block)
    {
        base.OnExchanged(block);
        GetBehavior<OnExchanged>()?.OnExchanged(block);
    }
}
