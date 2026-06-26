using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Toyota 93C66 EEPROM odometer algorithm.
/// Region 0x000–0x021: 17 × 2-byte rolling slots, no complement check.
/// Slot format: [ A, B ] — counter = A | (B &lt;&lt; 8), odometer_km = counter × 16.
/// Unused slots contain FF FF. Wrap detection: the value at the lowest address
/// is always current — post-wrap, the cluster overwrites from slot 0 upward.
/// </summary>
public class Toyota93C66 : EepromAlgorithm
{
    public override string Id    => "toyota-93c66";
    public override string Label => "Toyota 93C66";
    public override int ExpectedFileSize => 512;

    private const int OdoBase   = 0x000;
    private const int SlotCount = 17;
    private const int EntrySize = 2;
    private const int Divisor   = 16;

    public override int ReadOdometer(byte[] data)
    {
        // Scan forward; stop at the first FF FF (unused tail).
        // The value at the lowest address is always current:
        //   - No wrap  → all written slots share the same value.
        //   - Wrap     → the cluster overwrote from slot 0 with the new counter.
        ushort? firstValue = null;

        for (int i = 0; i < SlotCount; i++)
        {
            int off = OdoBase + i * EntrySize;
            byte a  = data[off];
            byte b  = data[off + 1];

            if (a == 0xFF && b == 0xFF)
                break;

            ushort val = (ushort)(a | (b << 8));
            firstValue ??= val;
        }

        if (firstValue == null)
            throw new InvalidDataException(
                "No valid odometer entries found — check you selected the correct EEPROM type.");

        return firstValue.Value * Divisor;
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        ushort counter = (ushort)(targetValue / Divisor);
        byte lo = (byte)(counter & 0xFF);
        byte hi = (byte)((counter >> 8) & 0xFF);

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < SlotCount; i++)
        {
            int off         = OdoBase + i * EntrySize;
            result[off]     = lo;
            result[off + 1] = hi;
        }

        return result;
    }

    public int NearestEncodable(int targetValue) => (targetValue / Divisor) * Divisor;
}
