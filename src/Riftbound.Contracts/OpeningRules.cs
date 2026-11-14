namespace Riftbound.Contracts;

public static class OpeningRules
{
    public const string Standard = "STANDARD";
    public const string S5CityMode1 = "S5_CITY_MODE1";
    public const string S5CityMode2 = "S5_CITY_MODE2";
    public const string S5CityMode3 = "S5_CITY_MODE3";

    public static bool IsValid(string? value) => value is Standard or S5CityMode1 or S5CityMode2 or S5CityMode3;
}

public sealed record OpeningSetupDto(string Stage, string FirstPlayerId, string SecondPlayerId,
    string? OwnBattlefieldCardNo, IReadOnlyList<string>? Sideboard);
