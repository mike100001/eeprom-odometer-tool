using OdometerTool.Models;

namespace OdometerTool.Algorithms;

public class HondaYNS93C86 : EepromAlgorithm
{
    public override string Id    => "honda-yns-93c86";
    public override string Label => "Honda YNS 93C86";
    public override int ExpectedFileSize => 2048;

    private const int PatternOffset = 0x1D8;
    private const int PatternSize   = 4;
    private const int Divisor       = 32;

    public override int ReadOdometer(byte[] data)
    {
        byte lo  = data[PatternOffset];
        byte hi  = data[PatternOffset + 1];
        byte nlo = data[PatternOffset + 2];
        byte nhi = data[PatternOffset + 3];

        VerifyChecksum(lo, hi, nlo, nhi, PatternOffset);

        int stored = lo | (hi << 8);
        return stored * Divisor;
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        int stored = targetValue / Divisor;
        byte lo  = (byte)(stored & 0xFF);
        byte hi  = (byte)((stored >> 8) & 0xFF);
        byte nlo = (byte)(~lo & 0xFF);
        byte nhi = (byte)(~hi & 0xFF);

        byte oldLo  = data[PatternOffset];
        byte oldHi  = data[PatternOffset + 1];
        byte oldNlo = data[PatternOffset + 2];
        byte oldNhi = data[PatternOffset + 3];

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i <= result.Length - PatternSize; i++)
        {
            if (result[i]     == oldLo  &&
                result[i + 1] == oldHi  &&
                result[i + 2] == oldNlo &&
                result[i + 3] == oldNhi)
            {
                result[i]     = lo;
                result[i + 1] = hi;
                result[i + 2] = nlo;
                result[i + 3] = nhi;
            }
        }

        return result;
    }

    public int NearestEncodable(int targetValue) => (targetValue / Divisor) * Divisor;
}
