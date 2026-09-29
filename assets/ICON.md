# SnipCanvas icon

The logo is drawn as vectors, not generated as a picture, so it stays sharp at every size and can be edited.

- `snipcanvas.svg` is the editable source.
- `snipcanvas.png` is the 1024 px artwork with a transparent background, used by the application header.
- `snipcanvas.ico` holds 16, 20, 24, 32, 40, 48, 64, 128 and 256 px versions. It is embedded in the Windows executable and used by the window, the system tray and the installer.
- `installer/wizard-*.bmp` is the artwork on the installer's welcome and header (a large side panel and a small header image, each at 1x and 2x).
- `build-icons.ps1` draws all of the above from one description of the design. Each icon size is drawn from the vectors instead of scaled down, and the smallest sizes get slightly heavier strokes so they survive pixel snapping.

To change the design, edit the `$Design` table at the top of `build-icons.ps1`, then run:

```
powershell -NoProfile -ExecutionPolicy Bypass -File assets\build-icons.ps1
```

Add `-Preview` to write a comparison sheet (`logo-preview.png`) of the sizes on light and dark backgrounds without changing the assets. Preview files are local diagnostics and must not be committed.

## The idea

The name is two things, and the mark shows both:

- **Snip:** four capture corners frame the picture, like the selection you drag before a capture.
- **Canvas:** a white page, tilted like a snipped photo, is the surface you mark up.
- **The mark:** a hand-drawn curved arrow in orange, the same color SnipCanvas uses for annotations by default.

The violet-to-indigo tile matches the app's accent color. There is no lettering, so the mark still reads at 16 px, and the orange arrow is the one thing that stands out on both light and dark taskbars.

The source artwork and generated icons are covered by the repository's MIT license.
