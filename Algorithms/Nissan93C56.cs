using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Nissan 93C56 EEPROM odometer algorithm (R34 GT-T).
/// Region 0x068–0x075: binary-weighted multi-slot up-counter, unrelated to the
/// [A, B, ~A, ~B] complement pattern used by other 93C56 variants (see HondaSubaru93C56).
/// Slot 0 (0x068, weight ×1) is the canonical source; slots 1–3 (0x06A/0x06C/0x06E,
/// weights ×2/×4/×8) are redundant copies at different binary weights. Slot 4 (0x070)
/// is a wrap-marker flag managed by the cluster and never written here. Slots 5–6
/// (0x072/0x074) are parity mirrors of slots 1–2, written as raw + 1.
/// Formula: km = (raw − 18626) × 160 / 3, where raw is an up-counter with a firmware
/// calibration baseline of 18626 at 0 km.
/// </summary>
public class Nissan93C56 : EepromAlgorithm
{
    public override string Id    => "nissan-93c56";
    public override string Label => "Nissan 93C56";
    public override int ExpectedFileSize => 256;

    private const int OdoBase  = 0x068;
    private const int Slot0    = OdoBase;        // ×1
    private const int Slot1    = OdoBase + 0x02; // ×2
    private const int Slot2    = OdoBase + 0x04; // ×4
    private const int Slot3    = OdoBase + 0x06; // ×8
    // Slot 4 (OdoBase + 0x08): wrap-marker flag, managed by cluster — never written.
    private const int Slot5    = OdoBase + 0x0A; // parity mirror of slot 1 (raw + 1)
    private const int Slot6    = OdoBase + 0x0C; // parity mirror of slot 2 (raw + 1)

    private const int RawZero  = 18626; // raw counter value at 0 km
    private const double ScaleNum = 160.0;
    private const double ScaleDen = 3.0;

    public override int ReadOdometer(byte[] data)
    {
        int raw0 = DecodeSlot0(ReadLE16(data, Slot0));
        int raw1 = DecodeSlot1(ReadLE16(data, Slot1));
        int raw2 = DecodeSlot2(ReadLE16(data, Slot2));

        if (raw0 != raw1 || raw0 != raw2)
            throw new InvalidDataException(
                $"Checksum mismatch at 0x{OdoBase:X3} — check you selected the correct EEPROM type.");

        return (int)Math.Round((raw0 - RawZero) * ScaleNum / ScaleDen);
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        int raw = (int)Math.Round(targetValue * ScaleDen / ScaleNum) + RawZero;

        byte[] result = (byte[])data.Clone();

        WriteLE16(result, Slot0, (ushort)(0xFFFF - raw));
        WriteLE16(result, Slot1, (ushort)(0xFFFF - raw * 2));
        WriteLE16(result, Slot2, (ushort)(0x1FFFE - raw * 4));
        WriteLE16(result, Slot3, (ushort)(0x2FFFD - raw * 8));
        // Slot 4 (flag/wrap marker): left untouched — managed by the cluster.
        WriteLE16(result, Slot5, (ushort)(0xFFFF - (raw + 1) * 2));
        WriteLE16(result, Slot6, (ushort)(0x1FFFE - (raw + 1) * 4));

        return result;
    }

    public int NearestEncodable(int targetValue)
    {
        int raw = (int)Math.Round(targetValue * ScaleDen / ScaleNum) + RawZero;
        return (int)Math.Round((raw - RawZero) * ScaleNum / ScaleDen);
    }

    private static int DecodeSlot0(ushort val) => 0xFFFF - val;
    private static int DecodeSlot1(ushort val) => (0xFFFF - val) / 2;
    private static int DecodeSlot2(ushort val) => (0x1FFFE - val) / 4;

    private static ushort ReadLE16(byte[] data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    private static void WriteLE16(byte[] data, int offset, ushort value)
    {
        data[offset]     = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}
