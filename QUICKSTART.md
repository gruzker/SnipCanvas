# SnipCanvas

A simple screenshot editor for Windows. Capture, annotate, copy, and share.

## Start

1. Run **SnipCanvas-Setup.exe** to install for your Windows account, then open SnipCanvas from Start. You can add a desktop shortcut during setup. No administrator access or separate .NET installation is needed. For portable use, extract **SnipCanvas-Windows-x64.zip** to a permanent folder and open **SnipCanvas.exe**. Keep its license notices alongside the executable.
2. Press **Print Screen**, or choose **Capture an area**, **Capture a window**, or **Full screen**. Press **Esc** to cancel.
3. Your screenshot saves in the background. A small bottom-right preview appears for three seconds (hover over it to keep it open); click it to open the editor and add arrows, boxes, text, blur, or crop. Ignore it to keep working. New captures are also copied automatically; use **Copy** after editing, or **Save** to save your edits. **Open folder** opens the screenshot folder selected in Settings, and the home screen lists your recent screenshots so you can reopen them.

If Windows opens its own snipping tool, disable **Use the Print Screen key to open screen capture** in Windows keyboard accessibility settings. Close other screenshot apps if Print Screen is unavailable.

The preview follows your light or dark app theme and slides down and fades in at the bottom right. Click **x** in its top-right corner to dismiss it immediately; your screenshot stays saved. The transition follows the Windows animation setting.

Change the capture key in **Settings > Capture shortcut**: click **Change**, press a new combination, and click **Save**.

## Your screenshots

Screenshots save immediately, and unsaved edits are saved before the next capture and when you quit. Click the tray icon to open your latest screenshot after the preview disappears. Open **Settings** (the sliders icon, or Ctrl+,) to choose the save folder, system, dark or light mode, automatic copying, and startup with Windows. **Open folder** opens your saved screenshots.

Choose a color and line thickness above the image for arrows and boxes. With the Text tool selected, drag existing text to move it. Select text to resize, recolor, edit, or delete it. Tool shortcuts: **A** arrow, **R** box, **T** text, **B** blur, **C** crop. **Ctrl+Z** undoes changes.

**Share** opens Windows Share or helps you paste or attach the PNG in WhatsApp, Telegram, or email. You choose the recipient and send it yourself.

Closing the window silently keeps SnipCanvas in the tray. Click the tray icon to reopen it. Choose **Quit SnipCanvas** from its tray menu to save and exit completely. Quit the old version before updating.

To remove an installed copy, quit it from the tray, then uninstall SnipCanvas in Windows Settings > Apps. Your saved screenshots and preferences are kept. Enable Start with Windows from the installed copy if you previously used the portable app.

## Compatibility

Windows x64, Windows 10 version 2004 or later. Release checks run on Windows 11; Windows 10 and mixed display scaling still need manual testing. Protected video may appear black. Blur is a visual effect and should not be relied on to permanently redact secrets.

This release is unsigned, so Windows may show an unknown-publisher prompt. SnipCanvas processes screenshots locally and does not upload them automatically.

Source and original artwork are MIT licensed. See the included `LICENSE`, `THIRD-PARTY-NOTICES.md`, and `licenses/` for redistribution notices.
