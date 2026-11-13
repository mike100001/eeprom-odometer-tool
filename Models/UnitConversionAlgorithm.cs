namespace OdometerTool.Models;

public enum DisplayUnit { Km, Miles }

/// <summary>
/// One config byte that differs between the KM and Miles software versions.
/// Only the bits in <see cref="Mask"/> are compared/changed, so other config
/// bits in the same byte are left as the car had them.
/// </summary>
public record BytePatch(int Offset, byte Mask, byte KmBits, byte MilesBits);

/// <summary>
/// Software KM ↔ Miles change. No odometer algorithm — just a fixed set of
/// config bits flipped between the two versions (taken from before/after dumps).
/// </summary>
public abstract class UnitConversionAlgorithm
{
    public abstract string Id { get; }
    public abstract string Label { get; }
    public abstract int ExpectedFileSize { get; }

    protected abstract IReadOnlyList<BytePatch> Patches { get; }

    public DisplayUnit ReadUnit(byte[] data)
    {
        if (Patches.All(p => (data[p.Offset] & p.Mask) == p.KmBits))
            return DisplayUnit.Km;

        if (Patches.All(p => (data[p.Offset] & p.Mask) == p.MilesBits))
            return DisplayUnit.Miles;

        string found = string.Join(", ", Patches.Select(p => $"0x{p.Offset:X3}={data[p.Offset]:X2}"));
        throw new InvalidDataException(
            $"Unrecognised KM/Miles config ({found}) — check you selected the correct EEPROM type.");
    }

    public byte[] WriteUnit(byte[] data, DisplayUnit unit)
    {
        byte[] result = (byte[])data.Clone();

        foreach (var p in Patches)
        {
            byte bits = unit == DisplayUnit.Km ? p.KmBits : p.MilesBits;
            result[p.Offset] = (byte)((result[p.Offset] & ~p.Mask) | bits);
        }

        return result;
    }
}
