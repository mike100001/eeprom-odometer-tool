using OdometerTool.Models;

namespace OdometerTool.Algorithms;

public class HondaYNS93C76 : EepromAlgorithm
{
    public override string Id    => "honda-yns-93c76";
    public override string Label => "Honda CRZ/RK5 93C76";
    public override int ExpectedFileSize => 1024;

    private const int BufferOffset = 0x1D8;
    private const int EntryCount   = 16;
    private const int EntrySize    = 4;
    private const int Divisor      = 32;

    public override int ReadOdometer(byte[] data)
    {
        int maxValue = -1;

        for (int i = 0; i < EntryCount; i++)
        {
            int off = BufferOffset + i * EntrySize;
            byte lo  = data[off];
            byte hi  = data[off + 1];
            byte nlo = data[off + 2];
            byte nhi = data[off + 3];

            if ((lo ^ nlo) != 0xFF || (hi ^ nhi) != 0xFF)
                continue;

            int miles = (lo | (hi << 8)) * Divisor;
            if (miles > maxValue)
                maxValue = miles;
        }

        if (maxValue < 0)
            throw new InvalidDataException(
                "No valid odometer entries found — check you selected the correct EEPROM type.");

        return maxValue;
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        int stored = targetValue / Divisor;
        byte lo  = (byte)(stored & 0xFF);
        byte hi  = (byte)((stored >> 8) & 0xFF);
        byte nlo = (byte)(~lo & 0xFF);
        byte nhi = (byte)(~hi & 0xFF);

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < EntryCount; i++)
        {
            int off = BufferOffset + i * EntrySize;
            result[off]     = lo;
            result[off + 1] = hi;
            result[off + 2] = nlo;
            result[off + 3] = nhi;
        }

        return result;
    }

    public int NearestEncodable(int targetValue) => (targetValue / Divisor) * Divisor;
}
