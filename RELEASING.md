# Releasing SnipCanvas

## First public source release

- Create an empty public repository using `main` as the default branch. This
  checkout starts with one clean Windows-only commit.
- Push `main`, enable Issues and private vulnerability reporting, and require
  the CI checks on pull requests. CI runs on pushes and pull requests.
- Confirm the repository license is detected as MIT and that its source archive
  contains only source, artwork, tests, build scripts, and documentation.
- Keep downloads in repository Releases. Do not commit installers, screenshots
  from your computer, preferences, or signing credentials.

## Windows binaries

1. Check the pinned .NET runtime against the
   [current support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
   Refresh `licenses/` from the matching runtime NuGet packages when it changes.
   .NET 8 support ends November 10, 2026; move to a supported runtime before then.
2. Set `<Version>` in `SnipCanvas.csproj` to the version being released.
   The first public app version is 1.0.0.
3. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php), then run from an
   interactive Windows desktop:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1
   ```

   Quit any copy running from this checkout first. An installed copy is separate.
   The script runs all nine regression checks and stages a new `dist` before
   replacing the previous output. It builds the installer and a portable ZIP
   containing the executable, guide, and license notices. Results and previews
   are in `.checks`; the generated release report is `dist/RELEASE-CHECK.md`.
   `powershell -NoProfile -File .\tests\ReleaseCheck.ps1` independently verifies
   checksums, portable ZIP contents, and bundled notices after packaging.
4. Inspect synthetic previews in both themes. Manually test capture, clipboard,
   sharing, upgrade/uninstall, Windows 10, and mixed-DPI displays. Existing
   screenshots and preferences must survive upgrades and uninstall.
5. If signing binaries, sign before packaging and regenerate SHA-256 checksums
   after signing. Verify on a clean Windows machine; state signing status in the
   release notes.
6. Upload `SnipCanvas-Windows-x64.zip`, `SnipCanvas-Setup.exe`, `SHA256.txt`, and
   `RELEASE-CHECK.md` to a release tagged `v1.0.0` (or the version being released).
   Publish the ZIP as the portable download so notices travel with the executable.
