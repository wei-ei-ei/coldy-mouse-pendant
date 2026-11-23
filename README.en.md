<p align="center">
  <a href="README.md">中文</a> · <b>English</b>
</p>

<p align="center">
  <br>
  <img src="assets/coldy-logo.png" alt="COLDY" width="320"/>
  <br>
  <span>Coldy is a Windows cursor pendant. The character hangs below the cursor, and the rope bends as the pointer moves.</span>
  <br>
</p>

<p align="center">
  <a href="#install">Install</a>
  &nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;
  <a href="#usage">Usage</a>
  &nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;
  <a href="#development">Development</a>
  &nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;
  <a href="#contributing">Contributing</a>
</p>

<p align="center">
  <a href="https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/latest"><img alt="release" src="https://img.shields.io/github/v/release/wei-ei-ei/coldy-mouse-pendant?label=release"></a>
  &nbsp;
  <img alt=".NET Framework 4.0 or newer" src="https://img.shields.io/badge/.NET%20Framework-4.0%2B-512BD4">
  &nbsp;
  <img alt="Windows" src="https://img.shields.io/badge/platform-Windows-0078D6">
</p>

This repository contains only one character, Coldy. The design comes from the snowman in the 《Colder》 music video: a round body, a striped scarf, a metal spoon on the head, a colorful cable on the right side, one closed eye, and one glowing blue eye.

## v1.7.0

- Removed the cocococo singing form: the form dropdown now offers only Form 1/2 and form4 is no longer loaded.
- Added four cursor schemes (System default / Ice / Pixel / Frost's Descent).
- Unified the cursor sizing API; fixed pixel cursors ignoring the size setting.
- Auto-update now triggers only when a newer published release exists in the repository.

## Install

The release package runs out of the box; no build environment is needed. The current version is [v1.7.0](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/tag/v1.7.0): [Coldy-1.7.0-win.zip](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/download/v1.7.0/Coldy-1.7.0-win.zip). The archive contains `Coldy.exe`, `Coldy.exe.config`, `assets/coldy.png`, `assets/coldy-form2.gif`, `assets/coldy-form4.gif`, and `assets/coldy-logo.png`.

1. Download the release package and extract it, keeping the directory layout intact.
2. Run `Coldy.exe`.
3. The pendant attaches to the system cursor and the settings window opens.

> [!NOTE]
> After downloading from a browser, Windows SmartScreen may show "Windows protected your PC". `Coldy.exe` is not code-signed, like most unsigned open-source releases. Click "More info", then "Run anyway".

Start at login: press `Win + R`, type `shell:startup`, and put a shortcut to `Coldy.exe` into that folder.

`Coldy.exe` loads `assets\coldy.png`, `assets\coldy-form2.gif`, and `assets\coldy-logo.png` from its own directory, and uses `Coldy.exe.config` to pick the .NET Framework 4 runtime. `assets\coldy-form3.gif` and `assets\coldy-form4.gif` (cocococo) are kept in the repository as draft/archive only and are no longer loaded by the program.

## Requirements

The build targets AnyCPU and runs on 32-bit and 64-bit Windows. The process declares system DPI awareness, so pendant coordinates and `GetCursorPos` share the same physical pixels.

| Dependency | Version | Notes |
| --- | --- | --- |
| Operating system | Windows 7 SP1 and later | Covers Windows 8, 8.1, 10, and 11 |
| Runtime | .NET Framework 4.0+ | 4.5 through 4.8 all work. Windows 10 1903 and Windows 11 ship with 4.8 built in |
| Release package | v1.7.0 | No installer |

On systems without .NET Framework 4, install the [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) runtime first.

## Usage

After startup there are three parts:

- The pendant following the cursor. The rope swings as the pointer moves and droops when idle.
- The settings window. Its title is the current character name.
- The system tray icon; the taskbar uses the same shrunken Coldy icon.
- Optional launch at login and optional background mode.
- Asks whether to update whenever a new GitHub Release is found.

The pendant is a topmost layered window with click-through enabled, so input passes to the applications below.

### Settings window

The window is a pale ice-colored rounded panel with a slightly small font, keeping only the controls themselves. The exact behavior of forms and cursors is documented in the table below and not repeated in the window. The top-left corner shows the fixed COLDY graffiti logotype instead of a character preview; hold that area to drag the window. The name and its font are not editable. The top-right corner has minimize and close. Inputs, dropdowns, buttons, and the color swatch share the same height.

| Item | Behavior |
| --- | --- |
| Rope length | 0–1000 pixels; the current value shows to the right of the slider. Longer ropes have more joints and still bend smoothly |
| Rope color | "Choose color" opens the color dialog with red, green, and blue from 0–255. The square next to the button shows the current color |
| Character size | Scales by the character's actual height; all three forms keep the same snowman display height, and the hang point scales along |
| Form | "Form 1" is the original still image. "Form 2" plays a crouch-jump, blinks, and a gentle sway using the same image. The singing form "cocococo" has been removed from the software: it is no longer offered in the dropdown and no longer loaded; its resource `assets/coldy-form4.gif` and generation scripts stay in the repository as an archive only, and legacy `form=3/4` configs fall back to Form 1. The old side-facing form 3 draft stays in `assets/coldy-form3.gif` |
| Cursor | The default "system default" uses the system pointers. "Ice" uses the Glacier cursor set: pale ice-surface blue gradient bodies, ink outlines and a white outer halo, switching between 14 forms (arrow, hand, I-beam, snowflake wait, resize, and more) depending on the control, legible on light and dark backgrounds alike. "Pixel" uses the animated pixel cursors by 线框xiankuang (16×16, 11-frame animation), nearest-neighbour scaled to size with the animation preserved, covering 16 forms (the I-beam was redrawn as a slim, centered, symmetric, shadow-free pixel glyph). "Frost's Descent" uses IP-material shape cursors: knit / felt / chrome / ice-crystal / rainbow-cable motifs carry the function (knit stretch = resize, spoon = help, snowball = move, scarf knot = unavailable), 14 shape states with micro-motion, no character and no face. Window edges get double arrows pointing in the matching direction. Design sources and an interactive preview live in `assets/cursors/` |
| Cursor size | Applies in "Ice", "Pixel" and "Frost's Descent" modes; the whole pointer set uses the same edge length |
| Hide hotkey | Records a toggle hotkey made of two keys. After clicking, press the two keys together or one after another; `Esc` cancels. Once recorded, pressing both keys hides or shows the character |
| Hide character | Toggles the pendant's visibility immediately |
| Launch at login | Writes to the current user's startup entries. After login it only appears in the tray without popping up the settings window |
| Background mode | Keeps the settings window off the taskbar. Closing or minimizing collapses it into the tray while the pendant keeps running. Once checked, the program can only be ended from the tray menu's "Exit" |
| Exit | Only in the tray menu. Ends the process and restores the system cursor |

Changes apply immediately and are written to `coldy.ini` next to the executable.

### Recording the hide hotkey

The default hotkey is `F8`. To record a new one:

1. Click "Hide hotkey". The button text becomes "Press two keys".
2. Press the two keys together or one after another. The UI first shows the first key, then the combination, for example `A + S`.
3. Press `Esc` during recording to cancel and keep the original hotkey.
4. The toggle fires once when both keys are held down at the same time. Holding them does not repeat.

The hotkey is global: it works even when the settings window has lost focus or the pendant is hidden.

### System tray

The tray icon and the taskbar button are both a shrunken Coldy. Double-click the tray icon to open the settings window.

| Menu | Behavior |
| --- | --- |
| Show console | Activates the settings window. Same as double-clicking the tray icon |
| Hide character / Show character | Toggles the pendant's visibility |
| Exit | Ends the process and restores the system cursor |

### Exit

"Exit" only exists in the tray menu; it ends the process and restores the system cursor. When "Background mode" is unchecked, the close button in the top-right corner also exits. When it is checked, closing and minimizing only collapse the window into the tray, and the settings window can no longer end the program.

Launching the program while it is already running opens the existing settings window instead of starting a second process.

Do not end `Coldy.exe` from Task Manager. Force-ending skips cursor restoration, and the custom cursors remain until the program exits normally again.

### Updates

At startup, and roughly every 10 minutes afterwards, the program checks the latest GitHub Release. It only asks when a release newer than the current version with a `Coldy-*-win.zip` asset is published. Choosing to update downloads the archive, exits, replaces the files in the same directory, and restarts. Choosing later keeps the current version. The repository must stay public so the program can read Releases without signing in.

## Runtime parameters

The slider ranges match the value ranges in the table below.

| Parameter | Config key | Range | Default | Notes |
| --- | --- | --- | --- | --- |
| Rope length | `rope` | 0–1000 | 120 | Resting rope length in pixels. The joint count grows with length |
| Rope color | `ropeColor` | `R,G,B`, each 0–255 | `55,55,62` | Rope color. The default is the current dark gray |
| Character size | `size` | 30–420 | 190 | Character display height in pixels |
| Form | `form` | `1` or `2` | `1` | `1` is the still image, `2` is `assets/coldy-form2.gif`. Legacy `3`/`4` (archived cocococo) fall back to `1` |
| Cursor | `cursor` | `scheme`, `pixel`, `felt`, or `system` | `system` | `system` uses the system pointers; `scheme` switches Glacier cursors by context; `pixel` uses the animated pixel cursors; `felt` uses the Frost's Descent shape cursors |
| Cursor size | `cursorSize` | 16–128 | 32 | Pointer edge length in pixels |
| Hide hotkey | `hotkey`, `hotkeyVk`, `hotkeyVk2` | Two distinct virtual key codes | `F8` | Toggles visibility when pressed together |
| Launch at login | `startup` | `0` or `1` | `0` | Also writes the current user's `Run` entry with the command `Coldy.exe --tray` |
| Background mode | `background` | `0` or `1` | `0` | `1` keeps the settings window off the taskbar; closing collapses it into the tray |

When `cursor` is `scheme`, the Glacier cursor set is used: an ice-gradient arrow in normal areas, a pointing hand with an ice-and-white striped cuff on links, a serif I-beam with a crystal insertion marker on text, a snowflake when busy, arrow-plus-badge cursors for working-in-background and help, one-piece double arrows on window edges, plus a precision cross, unavailable ring, move, and vertical alternate arrow — 14 forms in total. Every pointer is built from a white halo, an ink outline, and an ice-blue gradient core so it stays legible on light, dark, and colored backgrounds; hotspots align with the arrow tip pixel-for-pixel. When `cursor` is `pixel`, the animated pixel cursors by 线框xiankuang are used (16×16, 11 frames @ 12fps): each `.ani` is rebuilt at the target size with nearest-neighbour scaling and played natively by Windows, covering 16 pointer IDs including Person/Pin; the assets were extracted from the author's installer embedded ZIP (`tools/extract_pixel_pack.py`, the exe was never run). When `cursor` is `felt`, the Frost's Descent IP-material shape cursors are used: 14 shape/pattern states whose motifs carry the function (knit stretch = resize, chrome spoon = help, snowball = move, scarf knot = unavailable, ice crystal = wait, cyan dot = brand signature); animated states are encoded as `.ani` played natively by Windows and re-rendered vectorially at the target size, with no character and no face. `system` disables the cursor size slider and restores the system pointers. The legacy `arrow`, `hand`, `cross`, `ibeam`, and `dot` values in old configs read as `scheme`. See `assets/cursors/DESIGN.md` for the design spec.

The following are compile-time constants that the settings window cannot change:

| Constant | Value | Notes |
| --- | --- | --- |
| Rope segments | 2–28, varies with rope length | Roughly one joint per 40 pixels; the point mass count is segments plus one. Changing the length re-interpolates the current shape instead of snapping straight |
| Gravity | 11000 | Downward acceleration factor applied per simulation step |
| Fixed time step | 1/240 second | Verlet integration interval |
| Per-frame time cap | 0.033 second | Limits catch-up time after dropped frames |
| Per-frame step cap | 8 | Same as above |
| Refresh interval | About 8 milliseconds | The pendant's redraw period. The previous frame's area is cleared on the next frame to avoid ghosting while moving |
| Curve tension | 0.45 | Tension for `DrawCurve` |
| Hotkey polling interval | 30 milliseconds | Reads the pressed state of the two virtual keys |
| Hang offset | Active pointer size × 0.45 | Distance from the rope's top to the pointer hotspot. `scheme` uses the custom size; `system` uses the system pointer size |

`coldy.ini` is generated at runtime and not version-controlled. When it sits next to the executable, the rope length, rope color, size, form, pointers, hotkey, launch-at-login, and background mode are preserved. The name is fixed to `Coldy`; the legacy `name`, `font`, and `fontSize` keys in old files are ignored. Launch at login relies on the registry startup entry.

## Development

The implementation is C# 5 / WinForms targeting .NET Framework 4 with no NuGet dependencies. The UI, rope solver, and drawing live in `src/Program.cs`. `src/app.manifest` declares system DPI awareness plus compatibility with Windows 7 through Windows 10. `src/app.config` declares the .NET Framework 4.0 and newer runtimes.

The pendant window uses `WS_EX_LAYERED` and `WS_EX_TRANSPARENT`. Each frame draws the rope and character into a 32-bit premultiplied-alpha bitmap and submits it through `UpdateLayeredWindow`.

The form 2 animation is generated by `tools/MakeForm2Gif.cs` from `assets/coldy.png`. The old side-facing form 3 was removed from the software; `assets/coldy-form3.gif` and `tools/MakeForm3Gif.cs` remain as drafts. The singing form "cocococo" was also removed from the software (not offered in the dropdown, not loaded); `assets/coldy-form4.gif` and its generation scripts remain in the repository as an archive only.

The "cocococo" (archived; no longer loaded or offered) static base is rebuilt by `tools/build_form4_hd.py` from the frame at 41.2 seconds of the official 1440×2560 music video: in that frame the character sings facing the camera, the round mouth is centered right below the sunglasses, the microphone hugs the mouth with the rainbow cable drooping like an arm into the jack on the right side of the body, and the green-and-white and red-and-white knitted collar plus the blue-and-white long tail are all visible. The cutout uses rembg's isnet-general-use model (u2net bites a chunk out of the right side of the body in this frame), and gaps in the mask are recovered from the original frame with a "fluff-constrained growth" pass that only grows into bright, low-saturation plush pixels so the dark stage background cannot leak in. The singing animation is generated by `tools/animate_form4_singing.py`: the microphone stays fixed in the original pose, and a single smooth Gaussian displacement field pushes the microphone ball about 14 pixels toward the lower right so the mouth dot is fully revealed above the ball's upper edge (the cable stretches smoothly like a real cable while the end jack stays put); the mouth is a soft-edged ellipse sprite painted over the original mouth dot that covers it when closed and opens into a larger ellipse with the beat. The body is warped with **one** continuous `cv2.remap` pass — the rotation angle transitions smoothly along y between the head and body, combined with head nodding and local swinging of the cable arm, and every pixel is sampled only once, so the neck never shows ghosting from two frames. To reproduce the snow-pile-like fluffy texture, the white plush gets multi-octave noise bumps on top of brightening; stage artifacts on the silhouette — bright backlight halo, dark stage edge, and the reflective stage strip at the bottom — are repainted with the inner plush color without eroding the alpha or adding dark outlines, keeping thin structures like the spoon handle and the rainbow cable intact, and the body outline lands directly on the bright plush; the hem's alpha is smoothed as well so the edge does not ripple while swaying. The GIF palette comes from median-cut quantization of the frame pixels (plus a grayscale ramp) with error-diffusion dithering, so the scarf and collar gradients do not band. Finally, directional shading and two or three rising, fading notes are overlaid, and a 40-frame looping GIF is produced. The rope's segment count is chosen dynamically as 2–28 via `round(length/40)+2`, and `Rope.Resize` re-interpolates new joints while preserving the current shape.

`tools/clean_2d_mascot.py` strips the white sticker border outside the outline of the flat 2D artwork; `tools/make_logo.py` extracts the colorful graffiti logotype from the event poster; `tools/repair_form4.py` is the previous phone-screen-recording implementation, with its output kept at `assets/candidates/coldy-form4-screenrecord.gif`. These scripts need Python 3.13, OpenCV, Pillow, and rembg. The original post video, the full-body high-resolution sprite, the flat 2D artwork, and comparison images from every version all live in `assets/candidates/`.

All three forms are normalized by the real height of their opaque regions, `size` is the only size entry, and switching forms never jumps in scale. The rope anchor is computed from the currently active pointer size: `scheme`/`pixel` use "cursor size" while `system` uses `SystemInformation.CursorSize`, so enlarging a custom cursor and switching back to the system default does not keep a stale offset. The rope is a Verlet solver implemented inside the repository. Segment lengths are maintained by distance constraints, so the rope bends instead of stretching. The character's angle comes from the last segment's direction. Integration uses a fixed time step; fast pointer movement is limited by a per-frame step cap; after resting for a while the simulation freezes to avoid idle jitter. The hang point sits on the head's fluff, and the metal spoon never overlaps the rope.

The system pointers are drawn by the program. Choosing a style replaces the common pointer resources; choosing "system default" or exiting normally restores them through `SystemParametersInfo(SPI_SETCURSORS)`. A combination of two ordinary keys cannot be expressed with `RegisterHotKey`, so the hotkey polls `GetAsyncKeyState` and fires on the rising edge.

Build in the repository root:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /unsafe /target:winexe /platform:anycpu /win32manifest:src\app.manifest /win32icon:assets\coldy.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:Coldy.exe src\Program.cs
copy /Y src\app.config Coldy.exe.config
```

The executable and config output to the repository root so the sibling `assets` folder is loadable. `Coldy.exe` and `Coldy.exe.config` are not version-controlled. After changing `src/Program.cs`, double-click `build.bat` in the root to rebuild; if the pendant is running, "Exit" from the tray menu first, otherwise `Coldy.exe` is locked and the write fails.

```
src/Program.cs          Pendant, rope solver, settings window, cursors
src/app.manifest        DPI awareness and OS compatibility declarations
src/app.config          .NET Framework 4 runtime declaration
assets/coldy.png        Transparent sprite loaded at runtime
assets/coldy-logo.png   Colorful graffiti logotype for the README header
assets/coldy-avatar.png Full-body artwork used in documentation headers
assets/coldy-form3.gif  Old side-facing form draft, no longer loaded
assets/coldy-form4.gif  Singing form cocococo archive (no longer loaded/offered)
tools/MakeForm3Gif.cs   Generator script for the old side-facing form draft
tools/repair_form4.py   Rebuilds the singing form from a phone screen recording (previous version)
tools/build_form4_hd.py Rebuilds the singing form's static base from the official 1440x2560 video
tools/animate_form4_singing.py Generates the singing animation and notes with continuous warping
tools/clean_2d_mascot.py Removes the white sticker border from the flat 2D artwork
tools/make_logo.py      Extracts the graffiti logotype from the event poster
tools/MakeCursorSheet.cs Renders Glacier/pixel/Frost's Descent cursor PNGs and contact sheets from the production code
tools/make_cursor_svg.py Generates the Glacier cursor SVG design sources
tools/extract_pixel_pack.py Extracts the pixel cursor pack from the author's installer embedded ZIP (read-only)
assets/cursors/         Glacier, pixel and Frost's Descent cursor design sources, interactive preview, and contact sheets
assets/cursors/pixel/   Pixel cursor pack (17 animated .ani + install.inf, by 线框xiankuang)
assets/candidates/      Original post video, full-body sprite, flat artwork, and backups
屏幕录制 *.mp4           Singing form reference (phone screen recording)
build.bat               One-click rebuild of Coldy.exe
Coldy.exe               Build output, not version-controlled
Coldy.exe.config        Runtime config, not version-controlled
coldy.ini               Local config, not version-controlled
```

## Contributing

<a href="https://github.com/wei-ei-ei">
  <img src="https://github.com/wei-ei-ei.png" width="80" height="80" alt="wei-ei-ei"/>
</a>

<br>

[wei-ei-ei](https://github.com/wei-ei-ei)

<br>

<a href="https://github.com/wei-ei-ei/coldy-mouse-pendant/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=wei-ei-ei/coldy-mouse-pendant" alt="contributors"/>
</a>
