using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Mitsubishi Delica 93C86 KM ↔ Miles software change.
/// Config word at 0x5DC with its complement at 0x5DE:
///   KM    = 84 00 7B FF
///   Miles = 84 C0 7B 3F  (bits 6–7 of 0x5DD set, cleared in complement 0x5DF)
/// </summary>
public class MitsubishiDelica93C86Units : UnitConversionAlgorithm
{
    public override string Id    => "mitsubishi-delica-93c86-units";
    public override string Label => "Mitsubishi Delica 93C86";
    public override int ExpectedFileSize => 2048;

    protected override IReadOnlyList<BytePatch> Patches { get; } = new[]
    {
        new BytePatch(0x5DD, Mask: 0xC0, KmBits: 0x00, MilesBits: 0xC0),
        new BytePatch(0x5DF, Mask: 0xC0, KmBits: 0xC0, MilesBits: 0x00),
    };
}
