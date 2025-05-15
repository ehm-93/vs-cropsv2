using Vintagestory.API.Common;
using Vintagestory.API.Server;

class CropsV2Commands
{
    public static void Register(ICoreAPI api)
    {
        if (api is not ICoreServerAPI sapi) return;
        var cropsv2 = api.ChatCommands.Create("cropsv2")
            .WithDescription("Root command for interacting with CropsV2")
            .RequiresPrivilege(Privilege.controlserver);
        SetBlightCommand.Register(sapi, cropsv2);
        SetSporesCommand.Register(sapi, cropsv2);
        SetGenertionCommand.Register(sapi, cropsv2);
        SetWeedinessCommand.Register(sapi, cropsv2);
        cropsv2.Validate();
    }
}