using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AnyCommand]
    public sealed class ReAdminCommand : IConsoleCommand
    {
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;

        public string Command => "readmin";
        public string Description => "Re-admins you if you previously de-adminned.";
        public string Help => "Usage: readmin";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine("You cannot use this command from the server console.");
                return;
            }

            var mgr = IoCManager.Resolve<IAdminManager>();

            if (mgr.GetAdminData(player, includeDeAdmin: true) == null)
            {
                shell.WriteLine("You're not an admin.");
                return;
            }

            mgr.ReAdmin(player);
            _adminLogger.Add(LogType.Action, LogImpact.High, $"{player.Name} вернулся из DeAdmin");
        }
    }
}
