# EEPROM Odometer Tool — Blazor WebAssembly

## Overview

A C# Blazor WebAssembly web application that runs entirely in the browser. Users upload a dashboard EEPROM binary file, read the current odometer value, set a new value, and download the modified file. No server, no persistence — all processing happens client-side in-session.

---

## Tech Stack

- **C# / .NET 8**
- **Blazor WebAssembly** (client-side, no server required)
- No external packages needed — all binary manipulation uses native `byte[]`
- Hosting: GitHub Pages, Netlify, or any static host

## Project Setup

```bash
dotnet new blazorwasm -n OdometerTool
cd OdometerTool
code .
```

---

## Project Structure

```
OdometerTool/
├── Models/
│   └── EepromAlgorithm.cs          // abstract base + interface
├── Algorithms/
│   └── HondaAccordYNS93C76.cs      // first implementation
├── Services/
│   └── AlgorithmRegistry.cs        // hardcoded list of all algorithms
├── Pages/
│   └── Index.razor                 // main page (single page app)
├── Shared/
│   └── MainLayout.razor
└── wwwroot/
```

---

## UI Layout (Index.razor)

Two sections on the same page:

### Section 1 — EEPROM Odometer Tool

1. **Dropdown** — select EEPROM type (populated from registry)
2. **File upload** — `.bin` files only
   - Validates file size against selected algorithm
   - On valid upload: reads and displays current odometer value
3. **Current value display** — read-only, e.g. `Current value: 70,080`
4. **Target value input** — number input
   - Shows preview if nearest encodable value differs from input
   - e.g. `Will display: 9,984 (nearest encodable to 10,000)`
5. **Download button** — writes new value, triggers download of modified `.bin`
   - Output filename: `{original_name}_modified.bin`

### Section 2 — KM to Miles Calculator

- Two number inputs: **KM** and **Miles**
- Typing in either field instantly updates the other (live, no button)
- Conversion: `1 km = 0.621371 miles`, rounded to nearest whole number

---

## Models

### EepromAlgorithm.cs

```csharp
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
```

---

## Algorithm Registry

### AlgorithmRegistry.cs

```csharp
public static class AlgorithmRegistry
{
    public static readonly List<EepromAlgorithm> All = new()
    {
        new HondaAccordYNS93C76(),
        // add future algorithms here
    };
}
```

---

## Honda Accord YNS 93C76 Implementation

### HondaAccordYNS93C76.cs

```csharp
public class HondaAccordYNS93C76 : EepromAlgorithm
{
    public override string Id    => "honda-accord-yns-93c76";
    public override string Label => "Honda Accord YNS 93C76";
    public override int ExpectedFileSize => 1024;

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
```

---

## Index.razor — Key Logic

```razor
@page "/"
@inject IJSRuntime JS

<h2>EEPROM Odometer Tool</h2>

<select @onchange="OnAlgorithmChanged">
    @foreach (var algo in AlgorithmRegistry.All)
    {
        <option value="@algo.Id">@algo.Label</option>
    }
</select>

<InputFile OnChange="OnFileUpload" accept=".bin" />

@if (errorMessage != null)
{
    <p class="error">@errorMessage</p>
}

@if (currentValue.HasValue)
{
    <p>Current value: @currentValue.Value.ToString("N0")</p>
}

<input type="number" @bind="targetValue" @bind:event="oninput" />

@if (nearestEncodable.HasValue && nearestEncodable != targetValue)
{
    <p class="warning">
        Will display: @nearestEncodable.Value.ToString("N0")
        (nearest encodable to @targetValue)
    </p>
}

<button @onclick="DownloadFile" disabled="@(!CanDownload)">Download Modified File</button>

<hr />

<h2>KM to Miles Calculator</h2>
<input type="number" @oninput="OnKmChanged" @bind="kmValue" placeholder="KM" />
<input type="number" @oninput="OnMilesChanged" @bind="milesValue" placeholder="Miles" />

@code {
    private EepromAlgorithm selectedAlgorithm = AlgorithmRegistry.All[0];
    private byte[]? fileData;
    private string? fileName;
    private int? currentValue;
    private int targetValue;
    private int? nearestEncodable;
    private string? errorMessage;
    private bool CanDownload => fileData != null && currentValue.HasValue && targetValue > 0;

    private double kmValue;
    private double milesValue;
    private const double KmToMiles = 0.621371;

    private void OnKmChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out double km))
            milesValue = Math.Round(km * KmToMiles);
    }

    private void OnMilesChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out double miles))
            kmValue = Math.Round(miles / KmToMiles);
    }

    private void OnAlgorithmChanged(ChangeEventArgs e)
    {
        selectedAlgorithm = AlgorithmRegistry.All.First(a => a.Id == e.Value?.ToString());
        fileData = null;
        currentValue = null;
        errorMessage = null;
    }

    private async Task OnFileUpload(InputFileChangeEventArgs e)
    {
        errorMessage = null;
        currentValue = null;
        var file = e.File;

        if (file.Size != selectedAlgorithm.ExpectedFileSize)
        {
            errorMessage = $"Expected {selectedAlgorithm.ExpectedFileSize} bytes for {selectedAlgorithm.Label}, got {file.Size} bytes.";
            return;
        }

        using var stream = file.OpenReadStream();
        fileData = new byte[file.Size];
        await stream.ReadAsync(fileData);
        fileName = file.Name;

        try
        {
            currentValue = selectedAlgorithm.ReadOdometer(fileData);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            fileData = null;
        }
    }

    private async Task DownloadFile()
    {
        if (fileData == null) return;

        byte[] modified = selectedAlgorithm.WriteOdometer(fileData, targetValue);
        string outputName = Path.GetFileNameWithoutExtension(fileName) + "_modified.bin";

        await JS.InvokeVoidAsync("downloadFile", outputName, modified);
    }
}
```

---

## JS Interop — File Download

Add to `wwwroot/index.html` before `</body>`:

```html
<script>
  window.downloadFile = (filename, bytes) => {
    const blob = new Blob([new Uint8Array(bytes)], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  };
</script>
```

---

## Error States

| Condition | Message |
|---|---|
| Wrong file size | `Expected {n} bytes for {type}, got {m} bytes` |
| Checksum mismatch on read | `Checksum mismatch — check you selected the correct EEPROM type` |
| Pattern not found on write | `Odometer pattern not found in file` |
| Value out of range | `Value exceeds maximum for this EEPROM type` |

---

## Honda YNS 93C86

**Verified with:** Honda Accord/Civic YNS 93C86

**Same algorithm as Honda Accord YNS 93C76.** Only difference is file size:

| Property | 93C76 | 93C86 |
|---|---|---|
| File size | 1024 bytes | 2048 bytes |
| Offset | 0x1D8 | 0x1D8 |
| Divisor | 32 | 32 |
| Pattern | `[LO, HI, ~LO, ~HI]` x16 | `[LO, HI, ~LO, ~HI]` x16 |

Implement as a subclass or separate entry in the registry with `ExpectedFileSize = 2048`, reusing all other logic from `HondaAccordYNS93C76`.

Update the registry:

```csharp
public static readonly List<EepromAlgorithm> All = new()
{
    new HondaAccordYNS93C76(),
    new HondaYNS93C86(),  // same algo, ExpectedFileSize = 2048
};
```

---

## Note on Mid-Increment Reads

When a chip is read while the odometer is actively incrementing (engine running), the 16 redundant copies may contain two adjacent values. The read algorithm should always take the **highest** valid value. The write algorithm should write all 16 copies with the same value, normalising the file.

It would also be good practice to apply this same "take highest" logic retroactively to the 93C76 algorithm.

---

## Future EEPROM Types to Add

Each new algorithm needs:
- EEPROM chip type (e.g. 93C56, 93C66, 93C86)
- Vehicle make/model/year
- Expected file size in bytes
- Odometer offset(s)
- Encoding formula and divisor
- Number of redundant pattern copies

Known types to add:
- Honda Accord YNS 93C56 (var 1 & 2)
- Honda Accord YNS 93C66
- Honda Civic YNS 93C76 (var 1 & 2)
- Honda CR-V YNS 93C76 (var 1, 2 & 3)
- Honda Fit YNS 93C76
- Honda City YNS 93C76

---

## Notes

- All file processing is client-side — no data ever leaves the browser
- Files do not persist beyond the session
- Binary manipulation uses native `byte[]` — no external packages required
- This tool is for legitimate use cases: cluster swaps and km to miles conversion for JDM vehicle imports
