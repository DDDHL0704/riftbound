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
    string Trait,
    string EffectText,
    string RarityName,
    string ColorText,
    bool Visible,
    bool FaceDown,
    string ImagePath)
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

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(Trait))
            {
                details.Add(Trait);
            }

            if (!string.IsNullOrWhiteSpace(ColorText))
            {
                details.Add(ColorText);
            }

            if (!string.IsNullOrWhiteSpace(RarityName))
            {
                details.Add(RarityName);
            }

            if (details.Count > 0)
            {
                lines.Add(string.Join(" · ", details));
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

            if (!string.IsNullOrWhiteSpace(EffectText))
            {
                lines.Add(EffectText);
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
            ["trait"] = Trait,
            ["effectText"] = EffectText,
            ["rarityName"] = RarityName,
            ["colorText"] = ColorText,
            ["previewSummary"] = PreviewSummary
        };

        if (!string.IsNullOrWhiteSpace(CardName))
        {
            view["cardName"] = CardName;
        }

        if (!string.IsNullOrWhiteSpace(ImagePath))
        {
            view["imagePath"] = ImagePath;
        }

        return view;
    }
}
