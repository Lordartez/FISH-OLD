using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Database;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [UsedImplicitly]
    [AdminCommand(AdminFlags.None)]
    public sealed class DeAdminCommand : IConsoleCommand
    {
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        public string Command => "deadmin";
        public string Description => "Temporarily de-admins you so you can experience the round as a normal player.";
        public string Help => "Usage: deadmin\nUse readmin to re-admin after using this.";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine("You cannot use this command from the server console.");
                return;
            }

            var mgr = IoCManager.Resolve<IAdminManager>();
            mgr.DeAdmin(player);
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{player.Name} прописал ReAdmin");
        }
    }
}
