using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Paper;
using Content.Server.StationEvents.Components;
using Content.Shared.Paper;
using Content.Shared.SS220.Photocopier;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public class RandomSelector
{
    private readonly List<string> _options;
    private readonly List<string> _exclusions;

    public RandomSelector(List<string> options)
    {
        _options = options;
        _exclusions = new List<string>();
    }

    public string GetRandom(IRobustRandom robustRandom)
    {
        if (_exclusions.Count >= _options.Count)
            _exclusions.Clear();

        var availableOptions = _options.Except(_exclusions).ToList();
        if (availableOptions.Count == 0)
        {
            _exclusions.Clear();
            availableOptions = _options.ToList();
        }

        var selectedOption = robustRandom.PickAndTake(availableOptions);
        _exclusions.Add(selectedOption);

        return selectedOption;
    }
}

public sealed class CorporateLawChangeRule : StationEventSystem<CorporateLawChangeRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly FaxSystem _faxSystem = default!; // Добавлено для доступа к системе отправки факса

    private readonly RandomSelector _randomSelector;

    public CorporateLawChangeRule()
    {
        var options = new List<string>
        {
            "юбки и шорты",
            "кухонные ножи",
            "шляпы и береты",
            "зимние куртки",
            "алкогольные напитки",
            "несмешные клоуны",
            "еда с пряностями",
            "деятельность глав отделов, не уделяющих должного внимания патриотическому воспитанию своих сотрудников во славу NanoTrasen",
            "не ношение головного убора",
            "отсутствие личного приветствия со стороны глав отделов каждого члена экипажа, посещающего отдел",
            "нецензуруные выражения глав отделов в каналах связи"
            // Добавьте здесь остальные варианты сообщений
        };

        _randomSelector = new RandomSelector(options);
    }

    protected override void Started(EntityUid uid, CorporateLawChangeRuleComponent component,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var randomMessage = _randomSelector.GetRandom(_robustRandom);

        var messageKey = "station-event-corporate-law-change-announcement";
        var message = Loc.GetString(messageKey, ("essence", Loc.GetString(randomMessage)));

        _chat.DispatchGlobalAnnouncement(message, playSound: true, colorOverride: Color.Gold);

        // Отправляем факс с анонсом о корпоративном изменении законов
        SendCorporateLawChangeFax(message);
    }

    private void SendCorporateLawChangeFax(string message)
    {
        var faxes = EntityManager.EntityQuery<FaxMachineComponent>();
        foreach (var fax in faxes)
        {
            if (!fax.ReceiveStationGoal)
                continue;

            var dataToCopy = new Dictionary<Type, IPhotocopiedComponentData>();
            var paperDataToCopy = new PaperPhotocopiedData
            {
                Content = message,
                StampState = "paper_stamp-centcom",
                StampedBy = new List<StampDisplayInfo>
                {
                    new()
                    {
                        StampedName = Loc.GetString("stamp-component-stamped-name-centcom"),
                        StampedColor = Color.FromHex("#006600")
                    }
                }
            };
            dataToCopy.Add(typeof(PaperComponent), paperDataToCopy);

            var metaData = new PhotocopyableMetaData
            {
                EntityName = Loc.GetString("station-goal-fax-paper-name"),
                PrototypeId = "PaperNtFormCc"
            };

            var printout = new FaxPrintout(dataToCopy, metaData);
            _faxSystem.Receive(fax.Owner, printout, null, fax);
        }
    }
}
