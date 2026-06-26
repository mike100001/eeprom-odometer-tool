using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Subaru 93C76 EEPROM odometer algorithm.
/// Region 0x1A0–0x1BF: 8 × 4-byte rolling entries.
/// Entry format: [ A, B, ~A, ~B ] — counter = A | (B &lt;&lt; 8), odometer_km = counter × 16.
/// Encoding is identical to the 93C56 variant; only EEPROM size and ODO base offset differ.
/// </summary>
public class Subaru93C76 : EepromAlgorithm
{
    public override string Id    => "subaru-93c76";
    public override string Label => "Subaru 93C76";
    public override int ExpectedFileSize => 1024;

    private const int OdoBase    = 0x1A0;
    private const int EntryCount = 8;
    private const int EntrySize  = 4;
    private const int Divisor    = 16;

    public override int ReadOdometer(byte[] data)
    {
        int lastValidIndex = -1;

        for (int i = 0; i < EntryCount; i++)
        {
            int off = OdoBase + i * EntrySize;
            byte a  = data[off];
            byte b  = data[off + 1];
            byte na = data[off + 2];
            byte nb = data[off + 3];

            if ((a ^ na) != 0xFF || (b ^ nb) != 0xFF)
                continue;

            lastValidIndex = i;
        }

        if (lastValidIndex < 0)
            throw new InvalidDataException(
                "No valid odometer entries found — check you selected the correct EEPROM type.");

        int pos    = OdoBase + lastValidIndex * EntrySize;
        ushort ctr = (ushort)(data[pos] | (data[pos + 1] << 8));
        return ctr * Divisor;
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        ushort counter = (ushort)(targetValue / Divisor);
        byte a  = (byte)(counter & 0xFF);
        byte b  = (byte)((counter >> 8) & 0xFF);
        byte na = (byte)(a ^ 0xFF);
        byte nb = (byte)(b ^ 0xFF);

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < EntryCount; i++)
        {
            int off         = OdoBase + i * EntrySize;
            result[off]     = a;
            result[off + 1] = b;
            result[off + 2] = na;
            result[off + 3] = nb;
        }

        return result;
    }

    public int NearestEncodable(int targetValue) => (targetValue / Divisor) * Divisor;
}
