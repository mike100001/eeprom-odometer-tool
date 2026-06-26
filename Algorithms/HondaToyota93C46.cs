using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Honda/Toyota 93C46 EEPROM odometer algorithm.
/// Region 0x008–0x013: 3 × 4-byte redundant slots (identical copies, no rolling counter).
/// Slot format: [ b0, b1, b2, 0xFF ] — LE24 down-counter.
/// Formula: km = (0xFFF9B5 − LE24) × 17 / 107. Resolution ≈ 0.16 km/LSB.
/// Bytes 0x000–0x007 are cluster config — never modified.
/// </summary>
public class HondaToyota93C46 : EepromAlgorithm
{
    public override string Id    => "honda-toyota-93c46";
    public override string Label => "Honda/Toyota 93C46";
    public override int ExpectedFileSize => 128;

    private const int  OdoBase    = 0x008;
    private const int  SlotCount  = 3;
    private const int  SlotSize   = 4;
    private const uint C          = 0xFFF9B5u; // firmware zero-km baseline

    public override int ReadOdometer(byte[] data)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            int off = OdoBase + i * SlotSize;
            byte b0 = data[off];
            byte b1 = data[off + 1];
            byte b2 = data[off + 2];

            if (b0 == 0xFF && b1 == 0xFF && b2 == 0xFF)
                continue; // blank slot

            uint le24 = b0 | ((uint)b1 << 8) | ((uint)b2 << 16);
            return (int)Math.Round((C - le24) * 17.0 / 107.0);
        }

        throw new InvalidDataException(
            "No valid odometer entries found — check you selected the correct EEPROM type.");
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        uint le24 = (uint)Math.Round(C - targetValue * 107.0 / 17.0);
        byte b0 = (byte)(le24 & 0xFF);
        byte b1 = (byte)((le24 >> 8) & 0xFF);
        byte b2 = (byte)((le24 >> 16) & 0xFF);

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < SlotCount; i++)
        {
            int off         = OdoBase + i * SlotSize;
            result[off]     = b0;
            result[off + 1] = b1;
            result[off + 2] = b2;
            result[off + 3] = 0xFF;
        }

        return result;
    }
}
