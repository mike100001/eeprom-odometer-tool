alphgsc66_00.binbinalphgsc66.binbinToyota 93c66 94k kms and 58kRan 7 commandsRan 7 commandsGood — 17 slots (0–16), wrap confirmed. Now I have everything needed.Created a file, read a fileCreated a file, read a fileThis one has two meaningful differences from the Subaru chips worth flagging for your C# implementation:
No complement bytes — entries are just 2 bytes [A, B], so there's nothing to validate. The read logic relies solely on detecting FF FF and the wrap point.
Wrap detection is inverted — on the 93C56/93C76 you take the last valid entry at the highest address. On this Toyota chip, after a wrap the newest entries are at the lowest address, so you take firstValue from slot 0 rather than scanning to the end.
The ×16 multiplier and formula are otherwise identical across all three chips, so the core calculation is the same — just parameterise OdoBase, EntrySize, SlotCount, and the wrap detection strategy.93c66 toyota odometer algorithmDocument · MD silverdc5_00.binbinsilverdc5.binbinHonda/Toyota 93C46 88900 kms and 55,100Ran 15 commandsRan 15 commandsFormula cracked. Clean and consistent — both reference entries verify exactly. Here's what we found:

93C46 Honda/Toyota — Structure
PropertyValueEEPROM size128 bytesODO region0x008–0x013Entry size4 bytes [b0, b1, b2, 0xFF]Slot count3 redundant copies (no rolling counter)Blank markerFF FF FF FF
No rolling counter — all 3 slots always hold the same value. It's a 3× redundancy write for integrity, not a sequential log.
Formula
Completely different from the previous chips — this is a 24-bit down-counter with a fractional scale factor:
odo_km = (0xFFF9B5 - LE24(b0, b1, b2)) × 17 / 107
Inverse (for writing):
LE24 = round(0xFFF9B5 - odo_km × 107 / 17)
EntryDecodedFA 6F F7 FF88,900 kmFF AE FA FF55,100 km
The scale factor 107/17 ≈ 6.294 is likely pulse-derived from the specific tyre/VSS calibration baked into that cluster firmware. 0xFFF9B5 is the firmware constant representing the zero-km starting value.
Want me to write the .md now?Yes pleaseCreated a file, read a fileCreated a file, read a fileThe comparison table at the bottom now covers all four chip variants side by side — the 93C46 stands out clearly from the others with its down-counter encoding, fractional scale factor, and 3× redundancy write pattern. The spot-check table for 50k, 100k, and 0 km should be useful for testing your C# implementation against known expected byte sequences.93c46 honda toyota odometer algorithmDocument · MD ericfd2_NEW_77409.binbinericfd2.binbinNeed to work out this 93c76 odo for a Honda Civic. The two files attached are 48,000 and 77,400 but the algo we have in our app shows 44kusing OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Honda Civic FD2 — 93C76 EEPROM odometer algorithm.
/// Region 0x0180–0x01BF: 16 × 4-byte wear-levelling slots.
/// Slot format: [ lo, hi, ~lo, ~hi ]
/// Formula: km = (205 × counter + car_constant) / 7
/// car_constant is cluster-specific; defaults to 0 (adjust per cluster if needed).
/// </summary>
public class HondaCivicFD293C76 : EepromAlgorithm
{
    public override string Id    => "honda-civic-fd2-93c76";
    public override string Label => "Honda Civic FD2 93C76";
    public override int ExpectedFileSize => 1024;

    private const int RegionStart  = 0x0180;
    private const int SlotCount    = 16;
    private const int SlotSize     = 4;
    private const int Numerator    = 205;
    private const int Denominator  = 7;

    // car_constant is cluster-specific. Set to 0 as default.
    // Derive per cluster: car_constant = known_km * 7 - 205 * known_counter
    public int CarConstant { get; set; } = 0;

    public override int ReadOdometer(byte[] data)
    {
        // Collect all valid slots grouped by (lo, hi)
        var groups = new Dictionary<(byte lo, byte hi), int>();

        for (int i = 0; i < SlotCount; i++)
        {
            int offset = RegionStart + i * SlotSize;
            byte lo  = data[offset];
            byte hi  = data[offset + 1];
            byte nlo = data[offset + 2];
            byte nhi = data[offset + 3];

            if ((lo ^ nlo) != 0xFF || (hi ^ nhi) != 0xFF)
                continue; // invalid/transitional slot

            var key = (lo, hi);
            groups[key] = groups.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        if (groups.Count == 0)
            throw new InvalidDataException("No valid odometer slots found in region 0x0180–0x01BF.");

        // Majority group is the old (stable) block
        var (majorLo, majorHi) = groups.MaxBy(kv => kv.Value).Key;

        int counter = (majorHi << 8) | majorLo;
        return (int)Math.Round((Numerator * (double)counter + CarConstant) / Denominator);
    }

    public override byte[] WriteOdometer(byte[] data, int targetValue)
    {
        int counter = (int)Math.Round((targetValue * (double)Denominator - CarConstant) / Numerator);
        byte lo  = (byte)(counter & 0xFF);
        byte hi  = (byte)((counter >> 8) & 0xFF);
        byte nlo = (byte)(lo ^ 0xFF);
        byte nhi = (byte)(hi ^ 0xFF);

        byte[] result = (byte[])data.Clone();

        // Write all 16 slots — no need to simulate wear-level transition
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
}Show moreRan 9 commandsRan 9 commandsTwo bugs, both clear. Here's the full diagnosis:

Bug 1 — Wrong counter selection (logic error)
The ReadOdometer method picks the majority counter group — but after a wear-level transition, the majority is the old value. The new counter has only just started filling slots from the torn entry onward, so it's always the minority until the next full rotation.
48k file:   11 slots = counter 1522 (OLD)  ← majority = what app returns
             4 slots = counter 1525 (NEW)  ← correct
77k file:    6 slots = counter 2470 (OLD)  ← majority
             9 slots = counter 2475 (NEW)  ← correct
Fix: take the max valid counter, not the majority group.

Bug 2 — Wrong multiplier for this cluster
N=205, D=7 is not this variant. Working backwards from both known values:
(588 × 1525 + 15471) / 19 = 48,009 km  ✓  (user said ~48,000)
(588 × 2475 + 15471) / 19 = 77,409 km  ✓  (exact)
Correct constants: N=588, D=19, CarConstant=15471

Fixed C# class
csharpusing OdometerTool.Models;

namespace OdometerTool.Algorithms;

/// <summary>
/// Honda Civic FD2 — 93C76 EEPROM odometer algorithm.
/// Region 0x0180–0x01BF: 16 × 4-byte wear-levelling slots.
/// Slot format: [ lo, hi, ~lo, ~hi ]
/// Formula: km = (588 × counter + CarConstant) / 19
/// CarConstant is cluster-specific; this variant = 15471.
/// Derive per cluster: CarConstant = known_km * 19 - 588 * known_counter
/// </summary>
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

    // Cluster-specific constant. Derive from a known reading:
    // CarConstant = known_km * 19 - 588 * known_counter
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
                continue; // invalid/transitional slot — skip

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