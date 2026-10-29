using System.Security.Cryptography;
using System.Text;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public sealed record StandbyPlacement(string ObjectId, long SourceGeneration, string CardNo, string ControllerId,
    ObjectBinding Battlefield, int TurnNumber, long PlacedTick, string Permission, string PrintedTextSha256);

internal static class StandbyRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c=>c.CardNo,StringComparer.Ordinal));
    internal static bool HasPrintedKeyword(string? cardNo)
        => cardNo is not null && Cards.Value.TryGetValue(cardNo,out var card) && StandbyParser.HasPrintedKeyword(card.CardEffect);
    internal static int PlayedSelfMight(string? cardNo)
        => PlayedSelfEffect(cardNo)?.Might ?? 0;
    internal static StandbyPlayedSelfEffect? PlayedSelfEffect(string? cardNo)
        => cardNo is not null && Cards.Value.TryGetValue(cardNo, out var card)
            ? StandbyParser.SelfEffectWhenControllerPlaysFromFaceDown(card.CardEffect) : null;
    internal static bool AttachesSelfOnPlay(string? cardNo)
        => cardNo is not null && Cards.Value.TryGetValue(cardNo, out var card)
            && StandbyParser.AttachesSelfToFriendlyUnitHereWhenPlayedFromFaceDown(card.CardEffect);
    internal static bool EnemyPreventsPlayHere(MatchState state,string player,string field)
        => state.CardObjects.Values.Where(c=>FieldObjectTypeRules.IsVisibleUnit(c)
            && !CardControlRules.IsControlledByPlayerOrLegacyOwned(c,player)
            && state.ObjectLocations.TryGetValue(c.ObjectId,out var at) && at.Zone=="BATTLEFIELD" && at.BattlefieldObjectId==field)
            .SelectMany(c=>AttachedRuleTextRules.Instances(state,c))
            .Any(i=>Cards.Value.TryGetValue(i.CardNo,out var printed) && StandbyParser.PreventsEnemyFaceDownPlayHere(printed.CardEffect));
    internal static string TextHash(string? cardNo)
        => cardNo is not null && Cards.Value.TryGetValue(cardNo,out var card) ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(card.CardEffect??""))).ToLowerInvariant() : "";
    private static bool ValidFields(MatchState state,StandbyPlacement p)
        => !string.IsNullOrWhiteSpace(p.ObjectId) && !string.IsNullOrWhiteSpace(p.CardNo) && p.SourceGeneration>=0
            && !string.IsNullOrWhiteSpace(p.ControllerId) && state.Seats.ContainsKey(p.ControllerId)
            && p.TurnNumber>0 && p.TurnNumber<=state.TurnNumber && p.PlacedTick>0 && p.PlacedTick<=state.Tick
            && p.Battlefield is not null && !string.IsNullOrWhiteSpace(p.Battlefield.ObjectId) && p.Battlefield.Generation>=0
            && p.Battlefield.ObjectId!=p.ObjectId && p.PrintedTextSha256 is {Length:64} && p.PrintedTextSha256==TextHash(p.CardNo)
            && (p.Permission is "PRINTED" or "TEEMO" or "FREE") && HasPrintedKeyword(p.CardNo);

    internal static bool ValidLastPlay(MatchState state,CardObjectState card)
        => card.LastStandbyPlay is not { } p || ValidFields(state,p) && !card.IsFaceDown
            && p.ObjectId==card.ObjectId && card.ObjectGeneration==p.SourceGeneration+1 && state.TurnNumber>p.TurnNumber;

    internal static bool ValidPlayedOrigin(MatchState state,StackItemState item)
        => item.PlayedFromStandby is not { } p || ValidFields(state,p) && p.ObjectId==item.SourceObjectId && p.CardNo==item.CardNo
            && state.TurnNumber>p.TurnNumber && state.CardObjects.TryGetValue(item.SourceObjectId,out var source)
            && source.ObjectGeneration>=p.SourceGeneration+1
            && (source.ObjectGeneration!=p.SourceGeneration+1 || source.LastStandbyPlay==p);

    internal static bool AllowsBattlefieldEquipment(CardObjectState card,ObjectLocationState? at)
    {
        return card.LastStandbyPlay is { } origin && card.ObjectGeneration==origin.SourceGeneration+1
            && !card.IsFaceDown && at?.Zone=="BATTLEFIELD" && at.BattlefieldObjectId==origin.Battlefield.ObjectId;
    }
    internal static bool ValidPlacement(MatchState state, CardObjectState card)
        => card.StandbyPlacement is { } p && ValidFields(state,p) && card.IsFaceDown && p.ObjectId == card.ObjectId && p.CardNo == card.CardNo
            && p.SourceGeneration == card.ObjectGeneration && p.ControllerId == card.ControllerId
            && state.Seats.ContainsKey(p.ControllerId) && p.TurnNumber > 0 && p.TurnNumber <= state.TurnNumber
            && p.PlacedTick > 0 && p.PlacedTick <= state.Tick && p.Battlefield is not null
            && p.Battlefield.ObjectId != card.ObjectId && p.Battlefield.Generation >= 0
            && state.CardObjects.TryGetValue(p.Battlefield.ObjectId,out var field) && field.ObjectGeneration == p.Battlefield.Generation
            && field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)
            && state.ObjectLocations.TryGetValue(card.ObjectId,out var location) && location.Zone == "BATTLEFIELD"
            && location.BattlefieldObjectId == p.Battlefield.ObjectId && location.PlayerId == p.ControllerId
            && state.PlayerZones[p.ControllerId].Battlefields.Contains(card.ObjectId)
            && p.PrintedTextSha256 == TextHash(p.CardNo) && p.PrintedTextSha256.Length == 64
            && (p.Permission is "PRINTED" or "TEEMO" or "FREE") && HasPrintedKeyword(p.CardNo);
}
