using System.Collections.Generic;
using Godot;

namespace Riftbound.GodotClient;

internal sealed record CardViewData(
    string ObjectId,
    string CardNo,
    string CardName,
    string Category,
    int Energy,
    int Power,
    bool Visible,
    bool FaceDown,
    Image? Image)
{
    public string Label => Visible && !string.IsNullOrWhiteSpace(CardNo)
        ? string.IsNullOrWhiteSpace(CardName) ? CardNo : $"{CardNo}\n{CardName}"
        : "Hidden";

    public string PreviewSummary
    {
        get
        {
            if (!Visible || FaceDown)
            {
                return "Hidden card\nIdentity is hidden by the server snapshot.";
            }

            var title = string.IsNullOrWhiteSpace(CardName)
                ? CardNo
                : string.IsNullOrWhiteSpace(CardNo)
                    ? CardName
                    : $"{CardNo} · {CardName}";

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(title))
            {
                lines.Add(title);
            }

            if (!string.IsNullOrWhiteSpace(Category))
            {
                lines.Add(Category);
            }

            var stats = new List<string>();
            if (Energy >= 0)
            {
                stats.Add($"Cost {Energy}");
            }

            if (Power >= 0)
            {
                stats.Add($"Power {Power}");
            }

            if (stats.Count > 0)
            {
                lines.Add(string.Join(" · ", stats));
            }

            return lines.Count == 0 ? "Visible card" : string.Join("\n", lines);
        }
    }

    public Godot.Collections.Dictionary ToGodotDictionary()
    {
        var view = new Godot.Collections.Dictionary
        {
            ["label"] = Label,
            ["objectId"] = ObjectId,
            ["cardNo"] = CardNo,
            ["visible"] = Visible,
            ["faceDown"] = FaceDown,
            ["category"] = Category,
            ["energy"] = Energy,
            ["power"] = Power,
            ["previewSummary"] = PreviewSummary
        };

        if (!string.IsNullOrWhiteSpace(CardName))
        {
            view["cardName"] = CardName;
        }

        if (Image is not null)
        {
            view["image"] = Image;
        }

        return view;
    }
}
