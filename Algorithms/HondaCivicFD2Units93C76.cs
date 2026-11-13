using OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Honda Civic FD2 93C76 KM ↔ Miles software change.
/// Single config byte at 0x017: KM = DB, Miles = D7 (bits 2–3 swap).
/// </summary>
public class HondaCivicFD2Units93C76 : UnitConversionAlgorithm
{
    public override string Id    => "honda-civic-fd2-93c76-units";
    public override string Label => "Honda Civic FD2 93C76";
    public override int ExpectedFileSize => 1024;

    protected override IReadOnlyList<BytePatch> Patches { get; } = new[]
    {
        new BytePatch(0x017, Mask: 0x0C, KmBits: 0x08, MilesBits: 0x04),
    };
}
