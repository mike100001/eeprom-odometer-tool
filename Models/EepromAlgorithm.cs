namespace OdometerTool.Models;

public abstract class EepromAlgorithm
{
    public abstract string Id { get; }
    public abstract string Label { get; }
    public abstract int ExpectedFileSize { get; }

    public abstract int ReadOdometer(byte[] data);
    public abstract byte[] WriteOdometer(byte[] data, int targetValue);

    protected void VerifyChecksum(byte lo, byte hi, byte nlo, byte nhi, int offset)
    {
        if ((lo ^ nlo) != 0xFF || (hi ^ nhi) != 0xFF)
            throw new InvalidDataException(
                $"Checksum mismatch at 0x{offset:X3} — check you selected the correct EEPROM type.");
    }
}
