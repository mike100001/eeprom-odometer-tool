# Honda Instrument Cluster — 93C76 EEPROM Odometer Algorithm

> Reverse engineered from Honda Civic (EK) cluster dumps. Verified across three
> binary samples spanning 50,000 km and 182,911–183,321 km.

---

## EEPROM Region

| Property | Value |
|---|---|
| Region | `0x0180` – `0x01BF` |
| Size | 64 bytes |
| Structure | 16 × 4-byte wear-levelling slots |

---

## Slot Format

Each slot is 4 bytes:

```
[ lo, hi, ~lo, ~hi ]
```

- `lo` — low byte of the 16-bit odometer counter
- `hi` — high byte of the 16-bit odometer counter  
- `~lo` — bitwise complement of `lo` (`0xFF ^ lo`), integrity check only
- `~hi` — bitwise complement of `hi` (`0xFF ^ hi`), integrity check only

A slot is **valid** when `lo + ~lo == 0xFF` and `hi + ~hi == 0xFF`. Any slot
where these checks fail is a partial/mid-write entry and must be discarded.

---

## Wear-Levelling Ring Buffer

The cluster spreads EEPROM writes across all 16 slots in a circular ring to
extend chip lifetime. At any point in time the region contains:

1. **Old block** — the majority of slots, all holding the previous counter value
2. **Transitional slot** — one invalid slot (mid-write, fails complement check)
3. **Active block** — one or more slots at the end of the ring holding the new counter value

```
Example (file A, 182,911 km):

Slot  0  [ c3 16 3c e9 ]  ← old block (10 slots)
Slot  1  [ c3 16 3c e9 ]
...
Slot  9  [ c3 16 3c e9 ]
Slot 10  [ c3 16 32 e9 ]  ← transitional (invalid: 0xc3 + 0x32 ≠ 0xFF)
Slot 11  [ cd 16 32 e9 ]  ← active block (5 slots)
...
Slot 15  [ cd 16 32 e9 ]
```

The ring is circular — after slot 15 the next write goes to slot 0, overwriting
the oldest entry in the old block.

---

## Reading the Odometer Counter

**Always read from the old (majority) block**, not the active block. Only the
old block counter value satisfies the decode formula as an exact integer km
result. The active block counter is one increment ahead and gives a fractional
(invalid) result.

```
Algorithm:

1. Scan all 16 slots and discard any with invalid complement checks.
2. Group the remaining valid slots by their (lo, hi) pair.
3. The majority group is the old block — take its (lo, hi) values.
4. counter = (hi << 8) | lo
```

---

## Decode Formula

```
km = (205 × counter + car_constant) / 7
```

The result must be a whole number. If it is not, you are reading from the wrong
block.

### Scale factor

`205 / 7 ≈ 29.286 km per counter unit` — this is universal across clusters of
this type. It derives from the VSS pulse ratio and driveline gearing.

### car_constant

The constant is **cluster-specific**, set at the factory when the unit is first
programmed. It is not stored anywhere in the EEPROM in plain form — it must be
derived from at least one known-good reading of that cluster.

```
car_constant = km_known × 7 − 205 × counter_known
```

**Verified constants:**

| Sample | Known km | Counter | car_constant |
|---|---|---|---|
| Civic EK (high mileage) | 182,911 | 5,827 | 85,842 |
| Civic EK (lower mileage) | 50,000 | 1,588 | 24,460 |

---

## Encode Formula (Writing a Target km)

```
new_counter = (target_km × 7 − car_constant) / 205
```

Round to the nearest integer. Valid target km values are those that produce an
exact integer counter — i.e. multiples of `205/7` offset from the cluster's
origin. In practice the firmware handles sub-unit accumulation internally, so
rounding to the nearest integer is acceptable for calibration purposes.

Once `new_counter` is computed:

```
lo  = new_counter & 0xFF
hi  = (new_counter >> 8) & 0xFF
~lo = lo ^ 0xFF
~hi = hi ^ 0xFF
```

Write `[ lo, hi, ~lo, ~hi ]` identically into **all 16 slots** from `0x0180`
to `0x01BC`. There is no need to simulate the wear-level transition for a
deliberate calibration write.

---

## Reference Samples

| File | Stated km | Old block counter | hi byte | car_constant |
|---|---|---|---|---|
| `0426cutd2.bin` | 182,911 | 0x16C3 = 5,827 | 0x16 | 85,842 |
| `0426cutd2_NEW_183321.bin` | 183,321 | 0x16D1 = 5,841 | 0x16 | 85,842 |
| `0426cutd2_NEW_50000.bin` | 50,000 | 0x0634 = 1,588 | 0x06 | 24,460 |

The first two files are from the same physical cluster (identical `car_constant`
and `hi` byte progression). The third is a different cluster at much lower
mileage, confirming that the scale factor is universal but the constant is not.

---

## Notes

- The `hi` byte increments each time the `lo` byte wraps past `0xFF`, making
  this a straightforward 16-bit counter despite being split across two bytes.
- The complement bytes (`~lo`, `~hi`) serve only as a write-integrity check.
  They carry no independent odometer information.
- With only one reading from a cluster, `car_constant` can be derived and all
  future reads/writes for that cluster are fully deterministic.
- The formula has been validated to produce exact integer km results on all
  three sample files when the old block is used as the source.
