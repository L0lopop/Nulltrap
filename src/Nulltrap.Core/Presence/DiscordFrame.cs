using System.Buffers.Binary;
using System.Text;

namespace Nulltrap.Core.Presence;

public enum DiscordOpcode
{
    Handshake = 0,
    Frame = 1,
    Close = 2,
    Ping = 3,
    Pong = 4,
}

public static class DiscordFrame
{
    public const int HeaderLength = 8;

    public const int MaxPayloadLength = 64 * 1024;

    public static byte[] Encode(DiscordOpcode opcode, string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        byte[] body = Encoding.UTF8.GetBytes(json);

        if (body.Length > MaxPayloadLength)
        {
            throw new ArgumentException(
                $"A Discord frame may not exceed {MaxPayloadLength} bytes; this one is {body.Length}.",
                nameof(json));
        }

        var frame = new byte[HeaderLength + body.Length];

        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), (int)opcode);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), body.Length);
        body.CopyTo(frame.AsSpan(HeaderLength));

        return frame;
    }

    public static bool TryDecode(ReadOnlySpan<byte> frame, out DiscordOpcode opcode, out string json)
    {
        opcode = DiscordOpcode.Close;
        json = string.Empty;

        if (frame.Length < HeaderLength)
        {
            return false;
        }

        int code = BinaryPrimitives.ReadInt32LittleEndian(frame[..4]);
        int length = BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(4, 4));

        if (length < 0 || length > MaxPayloadLength || frame.Length < HeaderLength + length)
        {
            return false;
        }

        if (!Enum.IsDefined(typeof(DiscordOpcode), code))
        {
            return false;
        }

        opcode = (DiscordOpcode)code;
        json = Encoding.UTF8.GetString(frame.Slice(HeaderLength, length));

        return true;
    }
}
