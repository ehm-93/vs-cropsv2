using Ehm93.VintageStory.CropsV2;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

class SetSporesCommand
{
    private ICoreServerAPI Sapi;

    private SetSporesCommand(ICoreServerAPI sapi)
    {
        Sapi = sapi;
    }

    public static void Register(ICoreServerAPI sapi, IChatCommand parent)
    {
        var parser = sapi.ChatCommands.Parsers;
        parent.BeginSubCommand("set-spores")
            .WithAlias("spores")
            .WithDescription("Plant blight spores in the target farmland")
            .RequiresPrivilege(Privilege.controlserver)
            .RequiresPlayer()
            .WithArgs(parser.IntRange("spores", 0, 100))
            .HandleWith(new SetSporesCommand(sapi).Handle)
            .EndSubCommand();
    }

    public TextCommandResult Handle(TextCommandCallingArgs args)
    {
        var caller = args.Caller.Player;
        var target = caller.CurrentBlockSelection;

        BlockEntityFarmland farmland;
        if (target?.Block is BlockCrop) farmland = Sapi.World.BlockAccessor.GetBlockEntity<BlockEntityFarmland>(target.Position.DownCopy());
        else if (target?.Block is BlockFarmland) farmland = Sapi.World.BlockAccessor.GetBlockEntity<BlockEntityFarmland>(target.Position);
        else return TextCommandResult.Error($"Cannot run command, target must be BlockCrop or BlockFarmland, found {target.GetType().Name}");

        if (farmland == null) return TextCommandResult.Error("Cannot run command, no BlockEntityFarmland found.");

        var behavior = farmland.GetBehavior<BEBehaviorFarmlandBlight>();
        if (behavior == null) return TextCommandResult.Error("Cannot run command, no BEBehaviorFarmlandBlight found.");

        behavior.SporeLevel = (int)args[0];

        return TextCommandResult.Success();
    }
}