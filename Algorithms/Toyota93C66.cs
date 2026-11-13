using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Toyota 93C66 EEPROM odometer algorithm (verified against Vellfire dumps).
/// Region 0x000–0x021: 17 × 2-byte little-endian slots, no complement check.
/// The cluster increments one slot per unit in round-robin (wear levelling),
/// so the odometer is the SUM of all 17 slots, at 1 km/mile resolution.
///   e.g. 6 × 0x14DB + 11 × 0x14DA = 90,752 km
///        17 × 0x0CF5              = 56,389 miles
/// Slots therefore never differ by more than 1, and the higher ones come first.
/// </summary>
public class Toyota93C66 : EepromAlgorithm
{
    public override string Id    => "toyota-93c66";
    public override string Label => "Toyota 93C66";
    public override int ExpectedFileSize => 512;

    private const int OdoBase   = 0x000;
    private const int SlotCount = 17;
    private const int EntrySize = 2;

    public override int ReadOdometer(byte[] data)
    {
        int sum = 0;
        int min = int.MaxValue;
        int max = int.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            int off = OdoBase + i * EntrySize;
            int val = data[off] | (data[off + 1] << 8);

            if (val == 0xFFFF)
                throw new InvalidDataException(
                    $"Erased odometer slot at 0x{off:X3} — check you selected the correct EEPROM type.");

            sum += val;
            min = Math.Min(min, val);
            max = Math.Max(max, val);
        }

        if (max - min > 1)
            throw new InvalidDataException(
                "Odometer slots differ by more than 1 — check you selected the correct EEPROM type.");

        return sum;
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        // Spread the total across the slots: the first (target % 17) slots
        // get one extra, matching how the cluster itself increments them.
        int baseValue = targetValue / SlotCount;
        int remainder = targetValue % SlotCount;

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < SlotCount; i++)
        {
            int val         = baseValue + (i < remainder ? 1 : 0);
            int off         = OdoBase + i * EntrySize;
            result[off]     = (byte)(val & 0xFF);
            result[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        return result;
    }
}
