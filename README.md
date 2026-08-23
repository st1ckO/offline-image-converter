# Print Shop Image Converter

Print Shop Image Converter is an offline Windows desktop utility for turning customer-supplied images into easy-to-use JPG or PNG files. It is designed for batch work in a printing shop and does not upload files, require an account, or call an online service.

## Supported formats

Inputs:

- HEIC and HEIF
- AVIF
- WebP
- JPG and JPEG
- PNG
- TIFF
- BMP
- GIF

Outputs:

- JPG with adjustable quality, 4:4:4 sampling, optimized coding, and a white background for transparent pixels
- Lossless PNG with transparency and up to 16-bit channel depth

Animated and multi-image sources are exported as numbered still files. The app never changes source files or overwrites existing outputs.

## Shop workflow

1. Open **Print Shop Image Converter**.
2. Drop customer files or folders onto the queue, or use **Add files** and **Add folder**.
3. Check the detected format, dimensions, frame count, and preview.
4. Choose JPG or PNG, color handling, and an output folder.
5. Select **Convert batch**.
6. Use **Open** to view the completed output folder.

Folder intake examines files directly inside the selected folder and does not scan nested folders. Failed or unsupported files remain visible with an explanation so the rest of the batch can continue.

The default color mode converts embedded profiles to sRGB for predictable output in common print software. **Preserve source profile** is intended for a fully color-managed workflow. Metadata is retained where the destination format supports it.

## Local settings

The app remembers the last input location, output location, format, quality, and color choice in:

```text
%LocalAppData%\Print Shop Image Converter\settings.json
```

No telemetry or automatic update checks are implemented.

## Development

Requirements:

- Windows 10 or Windows 11 x64
- .NET 8 SDK or newer

Restore, build, and test:

```powershell
dotnet restore PrintShopImageConverter.sln --configfile NuGet.Config
dotnet build PrintShopImageConverter.sln --no-restore
dotnet test PrintShopImageConverter.sln --no-build --no-restore
```

Magick.NET is pinned to version 14.16.0. Tests generate their own image fixtures and cover intake, inspection, JPG/PNG conversion, color profiles, density, transparency, multi-frame export, collisions, progress, cancellation, settings, and view-model behavior.

## Release build

Create the tested, self-contained Windows x64 application:

```powershell
.\scripts\build-release.ps1 -SkipInstaller
```

The published application is written to:

```text
artifacts\publish\win-x64
```

To also create the installer, install Inno Setup 6 so `ISCC.exe` is available, then run:

```powershell
.\scripts\build-release.ps1
```

The installer is written to `installer\output`. It installs per user, does not require administrator privileges, includes Start Menu and optional desktop shortcuts, and supports normal Windows uninstallation.

## Privacy and limitations

- All processing is local and sequential.
- Existing outputs receive `_2`, `_3`, and later suffixes.
- Completed files remain when a later file fails or the batch is cancelled.
- Metadata retention is best-effort because JPG and PNG cannot represent every source field.
- Camera RAW, JPEG XL, PDF/TIFF output, editing, resizing, watched folders, and recursive folder scanning are outside version 1.

See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for bundled component attributions.
