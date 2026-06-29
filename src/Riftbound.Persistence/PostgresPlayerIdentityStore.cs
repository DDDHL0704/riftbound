using Npgsql;
using Riftbound.Engine;

namespace Riftbound.Persistence;

public sealed class PostgresPlayerIdentityStore(NpgsqlDataSource dataSource) : IPlayerIdentityStore
{
    public async ValueTask<PlayerIdentityStatus> ClaimOrVerifyAsync(
        string normalizedHandle,
        string keyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Atomic first-claim: the insert wins only when the handle is unclaimed.
        await using (var insert = new NpgsqlCommand(
            "insert into player_identity (handle, key_hash) values (@handle, @keyHash) "
            + "on conflict (handle) do nothing returning key_hash",
            connection))
        {
            insert.Parameters.AddWithValue("handle", normalizedHandle);
            insert.Parameters.AddWithValue("keyHash", keyHash);
            var inserted = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (inserted is not null)
            {
                return PlayerIdentityStatus.Registered;
            }
        }

        await using var select = new NpgsqlCommand(
            "select key_hash from player_identity where handle = @handle",
            connection);
        select.Parameters.AddWithValue("handle", normalizedHandle);
        var stored = (string?)await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return string.Equals(stored, keyHash, StringComparison.Ordinal)
            ? PlayerIdentityStatus.Verified
            : PlayerIdentityStatus.HandleClaimed;
    }
}
