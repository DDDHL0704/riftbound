using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    string ImagePath,
    bool IsExhausted = false,
    int? CurrentPower = null,
    int Damage = 0,
    bool IsStandby = false)
{
    public string Label => Visible && !string.IsNullOrWhiteSpace(CardNo)
        ? string.IsNullOrWhiteSpace(CardName) ? CardNo : $"{CardNo}\n{CardName}"
        : "隐藏卡牌";

    public string PreviewSummary
    {
        get
        {
            if (!Visible || FaceDown)
            {
                return "隐藏卡牌\n卡牌身份尚未公开。";
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
            if (IsStandby) lines.Add("待命 · 仍正面朝下");

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
                stats.Add($"费用 {Energy}");
            }

            if (CurrentPower is { } currentPower)
            {
                stats.Add($"当前战力 {currentPower}");
                if (Damage > 0) stats.Add($"已受伤害 {Damage}");
            }
            else if (Power >= 0)
            {
                stats.Add($"战力 {Power}");
            }

            if (stats.Count > 0)
            {
                lines.Add(string.Join(" · ", stats));
            }

            if (!string.IsNullOrWhiteSpace(EffectText))
            {
                lines.Add(Regex.Replace(EffectText, @"\{\{([^{}]+)\}\}", match =>
                {
                    var token = match.Groups[1].Value;
                    return token switch
                    {
                        "S" => "战力", "A" => "任意符能",
                        "红色" or "蓝色" or "绿色" or "黄色" or "紫色" or "橙色" => token + "符能",
                        _ => int.TryParse(token, out _) ? token + "法力" : token
                    };
                }));
            }

            return lines.Count == 0 ? "可见卡牌" : string.Join("\n", lines);
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
            ["isExhausted"] = IsExhausted,
            ["isStandby"] = IsStandby,
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

        if (CurrentPower is { } currentPower)
        {
            view["currentPower"] = currentPower;
            view["damage"] = Damage;
        }

        if (!string.IsNullOrWhiteSpace(ImagePath))
        {
            view["imagePath"] = ImagePath;
        }

        return view;
    }
}
