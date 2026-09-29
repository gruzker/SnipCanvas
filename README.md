<p align="center"><img src="assets/snipcanvas.png" alt="SnipCanvas logo" width="112"></p>

# SnipCanvas

An open-source screenshot app for Windows x64 that captures and edits locally. No account, server, or automatic uploads. Source and original artwork are [MIT licensed](LICENSE).

Built with C#, WPF, and .NET 8. Tested on Windows 11; Windows 10 version 2004 or later and mixed-DPI displays need manual verification. [CI](.github/workflows/ci.yml) builds and checks the Windows app.

## Get started

On Windows, install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then run from the repository root:

```powershell
dotnet run -c Release
```

For packaged downloads, use the repository's Releases page once a release is published. Choose `SnipCanvas-Setup.exe` to install, or extract `SnipCanvas-Windows-x64.zip` for portable use. Build artifacts are not committed to this repository. See [QUICKSTART.md](QUICKSTART.md) for the user guide and [CONTRIBUTING.md](CONTRIBUTING.md) to contribute.

New installations save to **Pictures\SnipCanvas**. No screenshots, preferences, or personal data are included in the source or release packages.

Capture an area, a window, or your main screen. Add arrows, boxes and text, blur an area, or crop your screenshot. Then copy it, share it, or save it as a PNG.

Every new screenshot saves immediately in the background. A small preview appears at the bottom right for three seconds without taking focus. Click it to open the screenshot in the editor, or keep working and let it disappear. Hovering over the preview pauses its countdown. You can also open the latest screenshot from the tray icon, or reopen any earlier one from **Recent screenshots** on the home screen. By default, screenshots are copied to the clipboard; automatic copying can be changed in Settings. Press Ctrl+V to paste. After editing, click **Copy** or press Ctrl+C to copy the updated version. Canceling a capture leaves the clipboard unchanged and does not reopen a hidden app.

SnipCanvas captures and edits locally, with no SnipCanvas account or automatic uploads. Each capture saves as a PNG in your configured screenshot folder. Starting a new capture automatically saves any unsaved edits to the previous screenshot. Each automatic save gets a unique filename. Screenshots already saved with no further edits are not saved again. Save PNG saves directly to that same configured folder; sharing creates a temporary PNG on your computer.

There is no discard prompt when starting another capture. If saving a new capture fails, SnipCanvas opens it in the editor and explains the error so you can save it elsewhere. If saving previous edits fails, the previous screenshot stays open and the new capture does not start.

## Share a screenshot

After capturing and editing, click **Share**:

- **Windows Share:** pass the edited PNG to Windows' share panel and choose a compatible installed app. Available destinations depend on the apps installed on your computer.
- **WhatsApp Web / Telegram Web:** copy the screenshot and open the web app. Choose a chat and press Ctrl+V, then review and send.
- **Email:** copy the screenshot and open a draft in your default email app. Paste the image if supported, or use **Show image file** to attach the PNG. The attachment is not added automatically.
- **Show image file:** open the PNG's folder so you can attach it or drag it into another app.

SnipCanvas does not select recipients or send messages. Shared copies include your edits and are stored in `%TEMP%\SnipCanvas\SharedImages`; they remain available for attachments until you or Windows removes them. If browser clipboard pasting is unavailable, use Show image file to attach the PNG instead.

## System tray and startup

Closing the window keeps SnipCanvas in the system tray, with your screenshot and edits still available. Print Screen continues to work. Click the SnipCanvas tray icon to reopen it, or right-click for **Open SnipCanvas**, **Capture an area**, **Capture a window**, **Capture full screen**, **Open screenshot folder**, **Settings**, and **Quit SnipCanvas**. The menu follows your light or dark theme. The icon may be inside Windows' hidden-icons menu.

Open **Settings** (the sliders icon in the app header, Ctrl+, or the tray menu), then turn on **Start with Windows** to launch SnipCanvas in the tray when you sign in. This is optional and is off by default. Clear the same option to disable it. Keep the executable in the same folder after enabling startup; if you move it, enable the option again from its new location.

Use **Quit SnipCanvas** to exit completely. Unsaved screenshots and edits are saved automatically to your screenshot folder before quitting, without a confirmation prompt. If saving fails, SnipCanvas keeps the screenshot open and shows the error so you can save it elsewhere.

## Open SnipCanvas

After building a release, open `dist/SnipCanvas.exe`, or open the executable from the extracted portable ZIP. Packaged Windows x64 builds include their runtime. Keep the guide and license notices alongside portable copies. When updating, choose **Quit SnipCanvas** from the tray menu before opening the new version.

## Capture and edit

The home screen offers the three capture modes, with your capture shortcut shown as a tip, and a grid of your **Recent screenshots**. Click one to open it in the editor. When a screenshot is open, the header offers **New screenshot** (click it for an area, or use the arrow beside it to choose a window or the full screen) and the toolbar below it holds the editing tools.

The capture preview follows your selected light or dark app theme, with a rounded thumbnail and soft shadow. It slides in at the bottom right, shows whether the screenshot was copied, and offers **Edit** (plus **Copy** and **Show in folder** when they apply). Click **x** to dismiss it without opening the editor or changing your saved image. It disappears after three seconds unless the pointer is over it. The transition follows the Windows animation setting.

Use the trash button to remove the current screenshot from the editor. Saved files and the clipboard are unchanged. **Undo** in the banner that appears restores the image and its edits until you take another screenshot or open another one.

Use **Open folder** at the bottom of the editor to open your current save location in File Explorer. This is also available on the home screen, in Settings beside Screenshot folder, and in the tray menu. If the folder does not exist yet, SnipCanvas creates it. The bottom bar also shows whether the screenshot is **Saved** or has **Unsaved edits**, and the image size.

- **Capture an area:** drag to select the area you want. A live size badge follows the selection. Press Esc or right-click to cancel.
- **Capture a window:** move over a window to see it highlighted with its title, then click to capture the whole window, excluding other windows covering it. SnipCanvas briefly brings the selected window forward to resume background video, then uses Windows Graphics Capture to include GPU-rendered content. Press Esc to cancel. Content protected by the app or Windows may still appear black.
- **Full screen:** capture your entire main screen.
- **Arrow (A):** drag from the arrow's tail to its tip.
- **Box (R):** drag to draw a rectangle around something.
- **Color and size:** while Arrow, Box or Text is active, choose a color and (for Arrow and Box) a line thickness above the image. Marks scale with the screenshot, so they stay visible on high-resolution displays.
- **Text (T):** click where you want the text. A compact floating panel opens nearby, with a live preview on the screenshot. Choose a color swatch or enter a custom `#RRGGBB` color. Press Enter or click Add text to apply; Shift+Enter adds a new line and Esc cancels. The last applied color is remembered for the current session.
- **Select and change text:** with the Text tool active, click an annotation to select it, then drag it to move. Use **A- / A+** to change its size (8-144 px), pick a swatch to recolor it, use **Edit** or double-click to change its wording and color, and **Delete** or the Delete key to remove it. Ctrl+Z undoes these changes. Text stays editable while the screenshot is open, including after cropping; exported PNGs contain the final image without selection outlines. Text already baked into an older screenshot is not selectable.
- **Blur (B):** drag over the area you want to blur.
- **Crop (C):** drag around the area you want to keep. Everything outside the selection is dimmed while you drag.
- **Undo / redo:** use the curved arrows, Ctrl+Z / Ctrl+Y, or Ctrl+Shift+Z.
- **Copy:** copy your edited screenshot to the clipboard, then paste it with Ctrl+V.
- **Save:** save your edited screenshot directly to the folder selected in Settings, with a unique filename. A confirmation offers **Show** to reveal the file.

Tool letters work whenever a screenshot is open. Below about 1000 px of window width the toolbar shows icons only; hover over a button for its name.

## Capture with Print Screen

To change the shortcut, open **Settings > Capture shortcut**, click **Change**, press your new combination, and click **Save**. Conflicting shortcuts are rejected. Your choice is remembered after restarting; **Use Print Screen** restores the default.

Keep SnipCanvas running, then press **Print Screen** to capture an area from another app. The shortcut also works while SnipCanvas is minimized or in the tray. Choosing **Quit SnipCanvas** releases the key.

The shortcut is shown at the bottom right of the window; click it to change it. If another app is using the key, a banner explains this and offers **Try again** and **Change shortcut**. Close other screenshot apps and extra SnipCanvas windows, then try again.

If Windows screen snipping opens instead, turn off **Settings > Accessibility > Keyboard > Use the Print Screen key to open screen capture**. You may need to sign out or restart Windows for the change to take effect. SnipCanvas does not change this setting on startup. Alt+Print Screen and Windows+Shift+S keep their existing behavior.

Within SnipCanvas, **Ctrl+N** starts an area capture, **Ctrl+C** copies the image, **Ctrl+S** saves to your screenshot folder, and **Ctrl+,** opens Settings. Esc returns from Settings to the editor. Editing and exporting preserve the image's pixel dimensions unless you crop it.

## Settings

Open **Settings > Appearance** and choose **System**, **Dark** or **Light**. System, the default for new installations, follows your Windows app theme and changes with it; earlier explicit choices are kept. Changing the theme does not change screenshot colors. Settings also contains the capture shortcut, automatic clipboard copying, the screenshot save folder, whether recent screenshots are shown on the home screen, starting with Windows, and a **Quit SnipCanvas** button. Switches apply immediately. Use **Change...** beside the screenshot folder to change where new screenshots are saved; existing files are not moved.

## Capture details

- Window capture excludes other windows covering the selected window. Minimized windows cannot be captured.
- Area capture can span multiple screens. Setups with different display scaling levels have not been manually verified.
- Protected video and Windows secure screens may not be captured.
- Blur softens details; it does not guarantee that sensitive information is unrecoverable.
- Arrows and boxes keep the color and thickness they were drawn with. Added text supports color, size, position, and wording changes.

Built and tested on Windows 11 x64. Windows 10 version 2004 or later is supported by the target framework but has not been manually tested. The app is unsigned.

## Build and verify

Building requires the .NET 8 SDK. The runtime version is pinned in `SnipCanvas.csproj`; review it for security updates before each release.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1` to run the existing diagnostics on an interactive Windows desktop. To package a release, install Inno Setup 6, quit any copy running from this checkout, and run `powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1`. It verifies the app, publishes a self-contained Windows x64 executable without test code, and builds the installer and portable ZIP with license notices. New output replaces `dist` only after packaging succeeds; test results are in `dist/RELEASE-CHECK.md`. See [RELEASING.md](RELEASING.md) for the publication checklist.

Checks cover image editing and PNG export, global shortcut registration and release, Escape cancellation, covered-window capture, tray lifecycle, automatic saving, sharing integration, editable text, and theme previews. The visual check renders the home screen (with and without recent screenshots), the editor with each tool, the settings page, menus and toasts in both themes, using synthetic screenshots only. Generated logs and previews go in `.checks`; inspect the previews before distributing a release. Tests do not send messages or change Windows startup settings.

For a normal source build, run `dotnet build -c Release`. Tests are maintained under `tests` and are included only with `-p:EnableDiagnostics=true`.

`dist` contains the latest release only. Build caches (`bin`, `obj`), `.checks`, and previews are ignored by Git. Machine-specific backups and signing credentials must stay outside the public repository.

## Installer

Run `dist/SnipCanvas-Setup.exe` for a per-user installation with a Start menu shortcut, optional desktop shortcut, and Windows uninstall entry. The default location is `%LOCALAPPDATA%\Programs\SnipCanvas`. Saved screenshots and preferences are retained on uninstall. Quit the app from its tray before updating or uninstalling.

The release script also builds the installer. Inno Setup 6 is required; use it under its applicable license terms. To package an already-built executable, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1`. The script accepts `-Compiler` for a custom ISCC.exe path. The portable executable, ZIP, and installer are the same app version; hashes are in `dist/SHA256.txt`.

## Contributing and license

Read [CONTRIBUTING.md](CONTRIBUTING.md) for setup, regression checks, and pull requests. Report ordinary bugs through Issues and suspected vulnerabilities as described in [SECURITY.md](SECURITY.md).

SnipCanvas is released under the [MIT license](LICENSE). Bundled runtime notices are documented in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
