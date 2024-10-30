// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using System.Linq;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

namespace Content.Shared.SS220.Photocopier.Forms;

[Serializable, DataDefinition]
public sealed partial class Form
{
    private const string DefaultPrototypeId = "paper";

    [DataField("content", required: true)]
    public string Content = "";

    [DataField("formId", required: true)]
    public string? FormId;

    [DataField("entityName")]
    public string EntityName = "";

    [DataField("photocopierTitle")]
    public string PhotocopierTitle = "Untitled";

    [DataField("prototypeId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>), required: true)]
    public string PrototypeId = "Paper";

    [DataField("stampState")]
    public string? StampState;

    [DataField("stampedBy")]
    public List<StampDisplayInfo> StampedBy = new();

    private Form()
    {
    }

    public Form(
        string entityName,
        string content,
        string? formId = null,
        string? photocopierTitle = null,
        string? prototypeId = null,
        string? stampState = null,
        List<StampDisplayInfo>? stampedBy = null)
    {
        EntityName = entityName;
        Content = content;
        FormId = formId;
        PrototypeId = prototypeId ?? DefaultPrototypeId;
        StampState = stampState;
        StampedBy = stampedBy ?? new List<StampDisplayInfo>();

        if (!string.IsNullOrEmpty(photocopierTitle))
            PhotocopierTitle = photocopierTitle;
    }
}

[Serializable, DataDefinition]
public partial class FormGroup
{
    [DataField("name", required: true)]
    public string Name = default!;

    [DataField("groupId", required: true)]
    public string GroupId = default!;

    [DataField("color", required: true)]
    public Color Color = default!;

    public readonly string? IconPath;

    [DataField("forms")]
    public Dictionary<string, Form> Forms = new();

    private FormGroup()
    {
    }

    public FormGroup(string name, string groupId, Color? color, string? iconPath)
    {
        Name = name;
        GroupId = groupId;
        Color = color ?? Color.White;
        IconPath = iconPath;
    }
}

[Serializable, DataDefinition]
public partial class FormCollection
{
    [DataField("collectionId", required: true)]
    public string CollectionId = default!;

    [DataField("groups")]
    public List<FormGroup> Groups = new();

    private FormCollection()
    {
    }

    public FormCollection(string collectionId)
    {
        CollectionId = collectionId;
    }
}
