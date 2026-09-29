# Contributing to SnipCanvas

Bug reports, documentation fixes, and focused pull requests are welcome.
Use the repository's Issues page for bugs and feature proposals. Include your
OS version, app version, display setup, reproduction steps, and expected behavior.
Use synthetic screenshots or remove private content before attaching images or logs.
Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md).

## Windows

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
on Windows, clone your fork, and run these commands from the repository root:

```powershell
dotnet build -c Release
dotnet run -c Release
```

Run the existing regression checks from an interactive Windows desktop:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1
```

Checks build a self-contained diagnostic executable and use temporary, synthetic
images. Some checks show windows, send Escape, and exercise screen capture, so
avoid typing into other apps while they run. Logs and previews go in `.checks`.
They do not send messages or change startup settings. The shipping build excludes
the diagnostic entry points. `check.ps1 -Checks self-test` runs the CI smoke check.

## Pull requests

Keep each change focused. Follow the surrounding code and reuse existing helpers
and native platform APIs. Explain the behavior changed and how you checked it;
add a small regression check when fixing nontrivial logic. UI changes should
include synthetic screenshots in both themes and preserve keyboard navigation
and accessibility labels. Never commit captures, settings, signing material, or
build output.

Contributions are licensed under [MIT](LICENSE). Treat contributors respectfully;
discuss the code and avoid personal attacks. See [RELEASING.md](RELEASING.md) for
maintainer release steps.
