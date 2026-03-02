using OdometerTool.Models;

namespace OdometerTool.Algorithms;

public class HondaYNS93C86BCD : EepromAlgorithm
{
    public override string Id    => "honda-yns-93c86-bcd";
    public override string Label => "Honda YNS 93C86 (BCD)";
    public override int ExpectedFileSize => 2048;

    private const int PatternOffset = 0x063;
    private const int PatternSize   = 3;
    private const int PatternStride = 4;   // every 4 bytes
    private const int PatternCount  = 3;   // 3 copies

    public override int ReadOdometer(byte[] data)
    {
        byte b0 = (byte)(~data[PatternOffset]     & 0xFF);
        byte b1 = (byte)(~data[PatternOffset + 1] & 0xFF);
        byte b2 = (byte)(~data[PatternOffset + 2] & 0xFF);

        int bcdHex = b0 | (b1 << 8) | (b2 << 16);

        // Read the hex representation as decimal digits
        return int.Parse(bcdHex.ToString("X6"));
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        // Encode decimal value as BCD hex
        string bcdStr = targetValue.ToString("D6");
        int bcdHex = Convert.ToInt32(bcdStr, 16);

        byte b0 = (byte)(~(bcdHex & 0xFF) & 0xFF);
        byte b1 = (byte)(~((bcdHex >> 8) & 0xFF) & 0xFF);
        byte b2 = (byte)(~((bcdHex >> 16) & 0xFF) & 0xFF);

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < PatternCount; i++)
        {
            int offset = PatternOffset + (i * PatternStride);
            result[offset]     = b0;
            result[offset + 1] = b1;
            result[offset + 2] = b2;
        }

        return result;
    }

    // BCD values are exact - no rounding needed
    public int NearestEncodable(int targetValue) => targetValue;
}
