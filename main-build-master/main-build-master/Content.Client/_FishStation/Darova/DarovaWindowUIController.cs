using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._FishStation.CCVar;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._FishStation.Darova;

[UsedImplicitly]
public sealed class WelcomeWindowUIController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly GameplayStateLoadController _gameplayStateLoad = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    private const string ContentsPath = "/ServerInfo/InGameDarova.xml";

    private DarovaWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        _gameplayStateLoad.OnScreenLoad += () =>
        {
            _window = UIManager.CreateWindow<DarovaWindow>();
            _window.LoadContents(new ResPath(ContentsPath));
            _window.NoShowButton.OnPressed += _ => OnDoNotShow();
        };

        _gameplayStateLoad.OnScreenUnload += () =>
        {
            if (_window != null)
            {
                _window.Dispose();
                _window = null;
            }
        };
    }

    public void OnStateEntered(GameplayState state)
    {
        var seen = _config.GetCVar(CCVars.WelcomePopupLastSeen);
        if (seen)
            return;

        _window?.Open();
    }

    private void OnDoNotShow()
    {
        _window?.Close();
        _config.SetCVar(CCVars.WelcomePopupLastSeen, true);
        _config.SaveToFile();
    }
}
