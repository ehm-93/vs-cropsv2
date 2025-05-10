using Ehm93.VintageStory.CropsV2;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

class SetWeedinessCommand
{
    private ICoreServerAPI Sapi;

    private SetWeedinessCommand(ICoreServerAPI sapi)
    {
        Sapi = sapi;
    }

    public static void Register(ICoreServerAPI sapi, IChatCommand parent)
    {
        var parser = sapi.ChatCommands.Parsers;
        parent.BeginSubCommand("set-weediness")
            .WithAlias("weed")
            .WithDescription("Spawn weeds in the target crop")
            .RequiresPrivilege(Privilege.controlserver)
            .RequiresPlayer()
            .WithArgs(parser.IntRange("weediness", 0, 100))
            .HandleWith(new SetWeedinessCommand(sapi).Handle)
            .EndSubCommand();
    }

    public TextCommandResult Handle(TextCommandCallingArgs args)
    {
        var caller = args.Caller.Player;
        var target = caller.CurrentBlockSelection;

        BlockEntityCropV2 crop;
        if (target?.Block is BlockCrop) crop = Sapi.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(target.Position);
        else if (target?.Block is BlockFarmland) crop = Sapi.World.BlockAccessor.GetBlockEntity<BlockEntityCropV2>(target.Position.UpCopy());
        else return TextCommandResult.Error($"Cannot run command, target must be BlockCrop or BlockFarmland, found {target.GetType().Name}");

        if (crop == null) return TextCommandResult.Error("Cannot run command, no BlockEntityCropV2 found.");

        var behavior = crop.GetBehavior<BEBehaviorCropWeeds>();
        if (behavior == null) return TextCommandResult.Error("Cannot run command, no BEBehaviorCropWeeds found.");

        behavior.WeedLevel = (int)args[0];

        return TextCommandResult.Success();
    }
}