using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record EquipmentKeywordGrantSpec(string RequiredKeyword, string GrantedKeyword);

public static class EquipmentKeywordGrantParser
{
    public static IReadOnlyList<EquipmentKeywordGrantSpec> Parse(string? text)
        => Regex.Matches(text ?? "", @"(?:^|\r?\n)你拥有\{\{(?<required>[^{}]+)\}\}的装备具有\{\{(?<granted>[^{}]+)\}\}。")
            .Select(match => new EquipmentKeywordGrantSpec(match.Groups["required"].Value, match.Groups["granted"].Value)).ToArray();
}
