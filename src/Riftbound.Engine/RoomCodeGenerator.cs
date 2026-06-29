using System.Security.Cryptography;

namespace Riftbound.Engine;

public static class RoomCodeGenerator
{
    private const string RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string NewRoomId()
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);

        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < bytes.Length; i++)
        {
            suffix[i] = RoomAlphabet[bytes[i] % RoomAlphabet.Length];
        }

        return $"RB-{new string(suffix)}";
    }
}
