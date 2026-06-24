using OdometerTool.Models;

namespace OdometerTool.Algorithms;

public class HondaYNS93C86 : EepromAlgorithm
{
    public override string Id    => "honda-yns-93c86";
    public override string Label => "Honda S660 93C86";
    public override int ExpectedFileSize => 2048;

    private const int RegionOffset = 0x1D8;
    private const int SlotCount    = 16;      // 16 × 4-byte slots = 64 bytes
    private const int SlotSize     = 4;
    private const int Divisor      = 32;

    public override int ReadOdometer(byte[] data)
    {
        // The region is a ring buffer. The ECU writes successive increments
        // into consecutive slots, so the CURRENT value is the maximum valid
        // slot (complement check must pass).
        int maxStored = -1;

        for (int i = 0; i < SlotCount; i++)
        {
            int pos  = RegionOffset + i * SlotSize;
            byte lo  = data[pos];
            byte hi  = data[pos + 1];
            byte nlo = data[pos + 2];
            byte nhi = data[pos + 3];

            if (nlo != (byte)(~lo) || nhi != (byte)(~hi))
                continue; // corrupt / erased slot — skip

            int stored = lo | (hi << 8);
            if (stored > maxStored)
                maxStored = stored;
        }

        if (maxStored < 0)
            throw new InvalidDataException("No valid odometer slot found in region.");

        return maxStored * Divisor;
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        // Round UP to nearest encodable multiple (matches OEM tool behaviour).
        int stored = (targetValue + Divisor - 1) / Divisor;
        byte lo    = (byte)(stored & 0xFF);
        byte hi    = (byte)((stored >> 8) & 0xFF);
        byte nlo   = (byte)(~lo);
        byte nhi   = (byte)(~hi);

        byte[] result = (byte[])data.Clone();

        // Overwrite ALL 16 slots unconditionally — don't rely on a pattern
        // match, because worn/bit-flipped slots won't match and would be
        // left with stale values.
        for (int i = 0; i < SlotCount; i++)
        {
            int pos       = RegionOffset + i * SlotSize;
            result[pos]   = lo;
            result[pos+1] = hi;
            result[pos+2] = nlo;
            result[pos+3] = nhi;
        }

        return result;
    }

    public int NearestEncodable(int targetValue) =>
        ((targetValue + Divisor - 1) / Divisor) * Divisor; // rounds UP, consistent with Write
}