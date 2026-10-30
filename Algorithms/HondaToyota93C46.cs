using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Honda/Toyota 93C46 EEPROM odometer algorithm.
/// Region 0x008–0x013: 3 × 4-byte redundant slots (identical copies, no rolling counter).
/// Slot format: [ b0, b1, b2, 0xFF ] — 24-bit little-endian, bitwise-inverted packed BCD.
/// Decode: value = ~LE24 &amp; 0xFFFFFF, then read each nibble as a decimal digit.
///   e.g. FF AD EE FF → LE24 0xEEADFF → inverted 0x115200 → 115,200 km.
/// Range 0–999,999 km, 1 km resolution.
/// Bytes 0x000–0x007 are cluster config — never modified.
/// </summary>
public class HondaToyota93C46 : EepromAlgorithm
{
    public override string Id    => "honda-toyota-93c46";
    public override string Label => "Honda/Toyota 93C46";
    public override int ExpectedFileSize => 128;

    private const int OdoBase   = 0x008;
    private const int SlotCount = 3;
    private const int SlotSize  = 4;
    private const int MaxValue  = 999_999; // 6 BCD digits

    public override int ReadOdometer(byte[] data)
    {
        ValidateSize(data);

        var values = new List<uint>(SlotCount);

        for (int i = 0; i < SlotCount; i++)
        {
            int off = OdoBase + i * SlotSize;
            byte b0 = data[off], b1 = data[off + 1], b2 = data[off + 2], b3 = data[off + 3];

            if (b0 == 0xFF && b1 == 0xFF && b2 == 0xFF && b3 == 0xFF)
                continue; // blank slot

            if (b3 != 0xFF)
                continue; // malformed slot: marker byte wrong, don't trust it

            values.Add((uint)(b0 | (b1 << 8) | (b2 << 16)));
        }

        if (values.Count == 0)
            throw new InvalidDataException(
                "No valid odometer entries found — check you selected the correct EEPROM type.");

        // Majority vote across the redundant copies instead of trusting slot 0.
        var best = values.GroupBy(v => v)
                         .OrderByDescending(g => g.Count())
                         .First();

        if (values.Count > 1 && best.Count() == 1)
            throw new InvalidDataException(
                $"Odometer slots disagree ({string.Join(", ", values.Select(v => $"0x{v:X6}"))}) — dump may be corrupt.");

        return Decode(best.Key);
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        ValidateSize(data);

        uint raw = Encode(targetValue);

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < SlotCount; i++)
        {
            int off = OdoBase + i * SlotSize;
            result[off]     = (byte)(raw & 0xFF);
            result[off + 1] = (byte)((raw >> 8) & 0xFF);
            result[off + 2] = (byte)((raw >> 16) & 0xFF);
            result[off + 3] = 0xFF;
        }

        return result;
    }

    /// <summary>Raw LE24 slot value → km.</summary>
    private static int Decode(uint raw)
    {
        uint bcd = ~raw & 0xFFFFFF;
        int value = 0;

        for (int shift = 20; shift >= 0; shift -= 4)
        {
            uint digit = (bcd >> shift) & 0xF;
            if (digit > 9)
                throw new InvalidDataException(
                    $"Slot value 0x{raw:X6} isn't valid inverted BCD (0x{bcd:X6}) — wrong EEPROM type or byte order?");
            value = value * 10 + (int)digit;
        }

        return value;
    }

    /// <summary>km → raw LE24 slot value.</summary>
    private static uint Encode(int km)
    {
        if (km < 0 || km > MaxValue)
            throw new ArgumentOutOfRangeException(nameof(km),
                $"Odometer value must be between 0 and {MaxValue:N0}.");

        uint bcd = 0;
        for (int shift = 0; shift <= 20; shift += 4)
        {
            bcd |= (uint)(km % 10) << shift;
            km /= 10;
        }

        return ~bcd & 0xFFFFFF;
    }

    private void ValidateSize(byte[] data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length != ExpectedFileSize)
            throw new InvalidDataException(
                $"Expected a {ExpectedFileSize}-byte dump, got {data.Length} bytes.");
    }
}