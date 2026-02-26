# Adding a New EEPROM Algorithm

## What You Need to Know First

Before writing code, gather the following for the target vehicle/EEPROM:

| Field | Description | Example |
|---|---|---|
| EEPROM chip | Chip model number | 93C76 |
| Vehicle | Make / model / year range | Honda Accord YNS 2003–2007 |
| File size | Expected `.bin` size in bytes | 1024 |
| Odometer offset | Hex offset of the first byte of the pattern | `0x1D8` |
| Pattern size | Number of bytes in the pattern | 4 |
| Encoding formula | How raw bytes map to a km/mile value | `(lo \| hi<<8) * 32` |
| Divisor | Granularity of encodable values | 32 |
| Checksum scheme | How the pattern is validated | `~lo, ~hi` (bitwise complement) |
| Redundant copies | Whether the pattern appears more than once | Yes — scan whole file |

---

## Steps

### 1. Create the algorithm class

Add a new file: `Algorithms/YourClassName.cs`

```csharp
public class YourClassName : EepromAlgorithm
{
    public override string Id    => "your-unique-id";       // kebab-case, e.g. "honda-civic-yns-93c76-v1"
    public override string Label => "Human Readable Label"; // shown in the dropdown
    public override int ExpectedFileSize => 1024;           // bytes

    private const int PatternOffset = 0x1D8; // change to correct offset
    private const int PatternSize   = 4;
    private const int Divisor       = 32;    // change to correct divisor

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
```

### 2. Register it

In `Services/AlgorithmRegistry.cs`, add the new instance to the list:

```csharp
public static readonly List<EepromAlgorithm> All = new()
{
    new HondaAccordYNS93C76(),
    new YourClassName(),   // <-- add here
};
```

---

## If the Algorithm Differs from the Template

### Different byte order / encoding
Override `ReadOdometer` with the correct formula. Examples:
- Big-endian: `int stored = (lo << 8) | hi;`
- 3-byte value: `int stored = lo | (hi << 8) | (b2 << 16);`

### Different checksum scheme
Override `VerifyChecksum` in the subclass, or add a separate validation method and call it from `ReadOdometer`.

### Single fixed offset (no scan needed)
Replace the scan loop in `WriteOdometer` with a direct write:
```csharp
result[PatternOffset]     = lo;
result[PatternOffset + 1] = hi;
result[PatternOffset + 2] = nlo;
result[PatternOffset + 3] = nhi;
```

### Multiple known fixed offsets
Write to each offset explicitly instead of scanning.

---

## Known Algorithms to Implement

| Label | Chip | File Size | Notes |
|---|---|---|---|
| Honda Accord Denso 93C86 | 93C86 | ? | Different manufacturer |
| Honda Accord YNS 93C56 (var 1) | 93C56 | ? | |
| Honda Accord YNS 93C56 (var 2) | 93C56 | ? | |
| Honda Accord YNS 93C66 | 93C66 | ? | |
| Honda Civic YNS 93C76 (var 1) | 93C76 | ? | |
| Honda Civic YNS 93C76 (var 2) | 93C76 | ? | |
| Honda CR-V YNS 93C76 (var 1) | 93C76 | ? | |
| Honda CR-V YNS 93C76 (var 2) | 93C76 | ? | |
| Honda CR-V YNS 93C76 (var 3) | 93C76 | ? | |
| Honda Fit YNS 93C76 | 93C76 | ? | |
| Honda City YNS 93C76 | 93C76 | ? | |

Fill in file size, offset, divisor, and any encoding differences as each is reverse-engineered.
