# win-plus

Windows 10 (1809+) / Windows 11 tray utility: touchpad gesture mapping,
per-device input rules, custom shortcuts, multi-profile screenshots
(with QR link detection), and a quick memo popup. x64 + x86. UI in 7 languages.

- Author: Yuki_Orita ([折田悠希](https://studio-rizi.pages.dev/))
- License: MIT (see `LICENSE`)
- Stable release (signed MSIX + portable zips): [GitHub Releases](https://github.com/oriyu90/win-plus/releases)
- Project page: https://studio-rizi.pages.dev/projects/win-plus/

## Install (users)

1. Download `win-plus_1.1.0_x64.msix` (or x86) from Releases.
2. Trust `win-plus-devcert.cer` (Trusted People, dev certificate), then open the `.msix`.
3. Or unzip the portable build and run `win-plus.exe`.

Requires Windows App Runtime 2.x (usually preinstalled via Store).
See `dist/README.md` in a release asset bundle for details.

## Build (developers)

- .NET SDK 10+, WinApp CLI 0.6+, Developer Mode on.
- `winapp new` not needed; build with `./BuildAndRun.ps1 .\win-plus.csproj -c Release --arch x64 --no-launch`
- Package: `winapp package <outdir> --cert <cert.pfx>` (framework-dependent; do NOT use `--self-contained` for packaged builds — it crashes at startup).
- Portable: `dotnet publish -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:SelfContained=true`

See `win-plus.md` for maintenance notes and `SPEC_AND_PLAN.md` for the original design.
