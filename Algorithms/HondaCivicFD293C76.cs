using OdometerTool.Models;

namespace OdometerTool.Algorithms;

public class HondaCivicFD293C76 : EepromAlgorithm
{
    public override string Id    => "honda-civic-fd2-93c76";
    public override string Label => "Honda Civic FD2 93C76";
    public override int ExpectedFileSize => 1024;

    private const int RegionStart = 0x0180;
    private const int SlotCount   = 16;
    private const int SlotSize    = 4;
    private const int Numerator   = 588;
    private const int Denominator = 19;

    public int CarConstant { get; set; } = 15471;

    public override int ReadOdometer(byte[] data)
    {
        int maxCounter = -1;

        for (int i = 0; i < SlotCount; i++)
        {
            int  offset = RegionStart + i * SlotSize;
            byte lo     = data[offset];
            byte hi     = data[offset + 1];
            byte nlo    = data[offset + 2];
            byte nhi    = data[offset + 3];

            if ((lo ^ nlo) != 0xFF || (hi ^ nhi) != 0xFF)
                continue; 

            int counter = (hi << 8) | lo;
            if (counter > maxCounter)
                maxCounter = counter;
        }

        if (maxCounter < 0)
            throw new InvalidDataException("No valid odometer slots found in region 0x0180–0x01BF.");

        return (int)Math.Round((Numerator * (double)maxCounter + CarConstant) / Denominator);
    }

    public override byte[] WriteOdometer(byte[] data, int targetKm)
    {
        int counter = (int)Math.Round((targetKm * (double)Denominator - CarConstant) / Numerator);
        byte lo  = (byte)(counter & 0xFF);
        byte hi  = (byte)((counter >> 8) & 0xFF);
        byte nlo = (byte)(lo ^ 0xFF);
        byte nhi = (byte)(hi ^ 0xFF);

        byte[] result = (byte[])data.Clone();

        for (int i = 0; i < SlotCount; i++)
        {
            int offset = RegionStart + i * SlotSize;
            result[offset]     = lo;
            result[offset + 1] = hi;
            result[offset + 2] = nlo;
            result[offset + 3] = nhi;
        }

        return result;
    }
}