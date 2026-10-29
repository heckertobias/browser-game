using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace BrowserGame.Server.Game;

/// <summary>Farbe pro Account: aus der Account-ID abgeleitet, gleich auf jedem Gerät und nach jedem Neustart.</summary>
internal static class PlayerColors
{
    private static readonly uint[] Palette =
        [0xE6194B, 0x3CB44B, 0xFFE119, 0x4363D8, 0xF58231, 0x911EB4, 0x46F0F0, 0xF032E6, 0xBCF60C, 0xFABEBE, 0x008080, 0xE6BEFF];

    public static uint For(string accountId) =>
        Palette[BinaryPrimitives.ReadUInt32BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(accountId))) % (uint)Palette.Length];
}
