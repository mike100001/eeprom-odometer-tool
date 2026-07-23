# EEPROM Odometer Tool

A client-side Blazor WebAssembly app for reading and editing the odometer value stored in dashboard cluster EEPROM dumps.

**Live site:** https://mike100001.github.io/eeprom-odometer-tool/

Upload a `.bin` EEPROM dump, pick the matching cluster/algorithm, read the current odometer value, set a new one, and download the modified file. Runs entirely in the browser — no file is ever uploaded to a server. Also includes a simple KM ↔ Miles live converter.

## Supported EEPROMs

| Cluster | Chip | File size |
|---|---|---|
| Honda CRZ/RK5 | 93C76 | 1024 bytes |
| Honda Civic FD2 | 93C76 | 1024 bytes |
| Honda S660 | 93C86 | 2048 bytes |
| Honda CW2 | 93C86 | 2048 bytes |
| Honda/Subaru | 93C56 | 256 bytes |
| Subaru | 93C76 | 1024 bytes |
| Toyota | 93C66 | 512 bytes |
| Honda/Toyota | 93C46 | 128 bytes |
| Nissan (Yazaki) | 93C56 | 256 bytes |

New clusters are added as classes in `Algorithms/`, extending `Models/EepromAlgorithm.cs`, and registered in `Services/AlgorithmRegistry.cs`.

## Access

The app is protected by a client-side password gate (SHA-256 checked, session-persisted). The password is injected at build time via a GitHub Actions secret and is not stored in the repo.

## Development

Requires .NET 10 SDK.

```bash
dotnet run
```

For local development, `wwwroot/appsettings.Development.json` sets the password to `password`.

## Deployment

Pushing to `master` triggers [.github/workflows/deploy.yml](.github/workflows/deploy.yml), which builds the app and publishes it to GitHub Pages.

## Tech stack

- .NET 10 / Blazor WebAssembly (client-side only, no backend)
