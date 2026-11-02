using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Api;

public static class SealedEndpoints
{
    public static IReadOnlyList<SealedSetDto> ListSets() =>
        SealedSetCatalog.Available
            .Select(set => new SealedSetDto(set.SetCode, set.Name))
            .ToArray();
}
