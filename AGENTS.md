# Privateer Agent Notes

## Active Stack
- Build the app in C# with WPF on the installed .NET Windows Desktop runtime.
- The active application project is `csharp/Privateer.Desktop`.
- Release packaging should use a self-contained publish plus an Inno Setup per-user installer with no admin requirement.

## Current Product Shape
- Windows 11-style screenshot app with a built-in editor.
- Theme-aware accent system:
- Neon green accent in `Light` and `Dark`
- Sepia-fitting accent colors in `Tan Sepia`, `Brown Sepia`, and `Green Sepia`
- Region capture overlay with:
- Greenshot-style full-screen selection
- Cyan dashed guide lines
- Circular zoom loupe near the cursor with centered white crosshair for placement accuracy
- Square-corner capture region outline
- `Esc` during capture mode should cancel the current selection and return to the previous app state without exiting Privateer
- Post-capture quick actions for `Save`, `Save As`, `Copy`, and `Open Editor`
- Preferred save folder and preferred filename support for one-click `Save`
- Capture hotkey selection and launch-on-startup preferences in the main window `Save Preferences` area
- `System`, `Light`, `Dark`, `Tan Sepia`, `Brown Sepia`, and `Green Sepia` theme support through shared styles plus swappable brush palettes
- Use the Privateer mark as the executable/window icon and incorporate the same mark into the main window hero section.
- When the user provides a specific Privateer logo file, use that exact asset for generated icon files and the hero-section branding instead of a traced approximation.
- The executable/taskbar icon should mirror the in-app hero badge proportions and crop closely, while also filling the taskbar icon space enough to feel comparable in footprint to the Greenshot taskbar icon.
- App windows should use the `.ico` asset for their window/taskbar icon path so the normal taskbar buttons match the tray/executable icon instead of falling back to a generic window glyph.
- Editor includes:
- Region highlight, rectangle, ellipse, line, arrow, freehand pen, text, speech bubble, counter, and obfuscation tools
- Rotate left/right and resize image operations
- Save, Save As, copy, undo, redo, and clear annotations

## Current Implementation Notes
- Final region images should be cropped from the pre-overlay full-screen screenshot, not captured again after the app window returns.
- Starting capture should not forcibly hide Privateer windows first; users need to be able to screenshot the app itself and simply cancel capture without side effects.
- Theme switching uses shared resource styles plus a full-width custom-themed popup-based selector control.
- Theme options should include `System`, `Light`, `Dark`, `Tan Sepia`, `Brown Sepia`, and `Green Sepia`, implemented as full shared brush palettes rather than one-off per-window overrides.
- Light and dark themes should keep the neon-green accent, while the three sepia themes should use their own warmer fitting accent colors instead of reusing neon green for primary buttons.
- The app should use a classic terminal-style font family across the UI, with shared theme resources driving that typography consistently instead of mixing in Windows variable UI fonts.
- Menus and menu flyouts should use shared themed templates rather than default WPF menu chrome where menus still exist elsewhere in the app.
- The editor should not show a separate top menu strip; keep app-level preferences in an `App Preferences` button inside the header bubble and remove the `Editor` label from that row.
- Opening the editor should hide the main app window without exiting the app, and each captured screenshot should open in its own editor window so multiple screenshots can be edited side by side. `App Preferences` from any editor should reopen the main window as a peer window that can come in front of the editors.
- Privateer should be able to run with no main window open via a tray icon in the Windows notification area, keep capture hotkeys active in the background, and expose `Preferences` and `Exit` from the tray icon context menu.
- The tray icon context menu should follow the currently active Privateer theme instead of using the default WinForms white menu chrome.
- The tray icon context menu should stay compact, center its menu labels, avoid hover highlight fills, and render its separator line flush across the full menu width.
- Tray menu theming should be fail-safe at runtime; changing app themes must not be able to crash Privateer because of tray color parsing or renderer updates.
- Capture hotkey registration should surface conflicts to the user, including in tray-first mode, instead of failing silently when another app already owns the shortcut.
- Privateer should launch tray-first by default: starting the executable should keep the main/preferences window hidden until the user explicitly opens preferences, and closing the editor should not automatically reopen the main/preferences window.
- After a successful capture, keep the main window hidden by default: if quick actions are enabled show only the `Capture Ready` dialog, otherwise open only the editor window. Do not also reopen the main window.
- Capture overlays must remain non-blocking for pointer input except for the main overlay surface that owns the drag gesture.
- The capture screen should leave the captured desktop visually unchanged and only draw the selector UI on top of it, so users can see the region exactly as it will be captured regardless of theme.
- The capture region edge should align directly with the crosshair position, so the visible selector border matches the actual captured bounds.
- The zoom loupe should be rendered as a true centered ellipse so the white outline and zoomed content stay perfectly aligned.
- Capture overlay pointer motion should feel smooth like Greenshot; avoid rebuilding heavy magnifier content on every raw mouse event and prefer frame-coalesced updates so the crosshair keeps up with the cursor.
- The editor uses a compact direct-tool-button layout with an `App Preferences` header button rather than a separate top menu strip or editor-side combo boxes.
- The main window should stay focused on capture preview and save preferences; avoid extra workflow explainer cards or a persistent bottom status strip.
- The main window should open in a compact desktop size closer to the current reference layout, not an oversized near-fullscreen default.
- Main window preferences should include real persisted settings for capture hotkey choice and Windows startup launch behavior, not placeholder controls.
- The capture hotkey picker should offer only `Print Screen` and `Custom`; when `Custom` is selected, show a field directly below it that records the user’s chosen key or key combination.
- The normal startup window height must already be tall enough to show the full `Save Preferences` button, and switching hotkey mode back from `Custom` to `Print Screen` should return the window to that normal height.
- The main window `Save Preferences` card should always be tall enough for every preference control and the save button, with no overlap or clipping between rows.
- The `Save Preferences` card should also fit fully within the compact startup window height, so prefer slightly tighter internal spacing there over growing the main window again.
- In the main dashboard layout, the `Latest Capture` and `Save Preferences` cards should share the same bottom alignment; the preferences card should stretch with the column and keep its save button anchored near the bottom.
- The `Save Preferences` description text should wrap with the card width and adapt to window resizing instead of clipping at the right edge.
- The main window minimum height should stop vertical resizing before the `Save Preferences` button or other bottom controls can be pushed out of view.
- When `Capture hotkey` is switched to `Custom`, the main window should automatically increase its minimum height enough to reveal the custom hotkey field and keep the `Save Preferences` button visible.
- The latest-capture detail text in the main window should wrap onto multiple lines when needed instead of bleeding into the Save / Save As / Copy / Open Editor buttons.
- Editor control event handlers must tolerate `InitializeComponent` ordering, because slider or similar value-changed events can fire before the drawing surface controls are fully ready.
- The editor left rail should use a compact card and button density so the `Tools` with inline hint text, `Image`, and `Style` sections fit in the default window without a vertical scrollbar.
- The editor `Style` card should only expose color and thickness; text and speech bubble tools now use built-in default labels instead of a persistent input field.
- The editor color chips should stay limited to the current six options, but use saturated, high-contrast versions of those colors rather than softer pastel variants.
- The selected editor color chip should indicate selection with that chip's own color ring and sizing, not with the global neon green accent.
- Editor tool-button and color-chip selection visuals should stay bound to live theme resources so an already-open editor updates cleanly when the app theme changes.
- The editor header should stay compact and toolbar-like rather than using a tall hero card.
- Editor image operations like rotate, resize, and region obfuscation should preserve the current annotation state cleanly so undo, redo, and `Clear All` remain reliable after edits.
- Editor `Clear All` should restore the original captured image and remove every editor change, including rotations, resizes, obfuscation, strokes, and annotations.
- The editor highlight tool should behave like a rectangle-region marker with a translucent fill and no final outline, while the obfuscation tool should immediately pixelate the selected region without leaving a border behind.
- Text and speech-bubble annotations should enter inline edit mode when placed, and re-clicking an existing one with the matching tool should edit its text instead of spawning a duplicate static annotation.
- Speech-bubble editing should resize live while the user types so the editable text area and final bubble keep pace with the content instead of only expanding after commit.
- Speech bubbles should grow in both width and height from normal typing and soft wraps, not only from explicit `Enter` newlines, and should stop accepting additional text once they hit the configured size/character cap.
- Plain text annotations should wrap instead of running off the image, using a roughly 50-character line width and the same overall character cap as speech bubbles.
- Editor arrows should land the head directly on the selected endpoint, with the shaft terminating inside the head so the mark reads as one continuous arrow instead of a line plus a slightly offset triangle.
- Editor utility dialogs like `Resize Image` should open large enough to show every input, checkbox, and action button without clipping at default DPI.
- The editor should open as a medium desktop window by default rather than near-fullscreen, with just enough default height to avoid left-rail overflow in the normal layout.
- The editor left tools pane may remain scrollable when the window is manually shrunk, but its scrollbar should stay visually hidden.
- The main editor canvas should use a dedicated themed `ScrollViewer` template that matches the reference look: slim dark rails, a light rounded thumb, rounded triangle arrows at the true ends, and no extra boxed corner chrome.
- Editor canvas scrollbars should reserve only thickness, stretch the full visible canvas edge, place arrow buttons at the true ends of each bar, collapse fully whenever the image already fits in the viewport, and avoid boxed button styling around the arrows.
- Editor canvas scrollbar rails and thumbs should use a uniform pill thickness with fully rounded ends in both horizontal and vertical orientations.
- Use one shared visual thickness for the entire editor scrollbar system so the rail, thumb, and end placements do not create a wider outer gutter around a narrower pill.
- Scrollbar arrow glyphs should stay clearly triangular even when softened; prefer rounded triangle silhouettes over abstract blob-like shapes.
- Scrollbar arrow buttons should visually match the scrollbar thickness so the end glyphs feel like part of the same 8px system rather than oversized attachments.
- Ghost-style editor buttons should switch to dark accent text on hover as soon as the active theme accent hover fill appears, not only after selection.
- Tool buttons should not keep a local unselected foreground override, so hover text can switch to dark accent text consistently in the `Tools` section too.

## Project Layout
- `csharp/Privateer.Desktop/Windows` for WPF windows
- `csharp/Privateer.Desktop/Services` for capture, settings, save, clipboard, and theme services
- `csharp/Privateer.Desktop/Models` for app state and editor data contracts
- `csharp/Privateer.Desktop/Resources` for shared styles and light/dark brush dictionaries
- `csharp/Privateer.Desktop/Interop` for Win32 and DWM helpers

## Working Agreements
- Prefer modular services and focused helpers over very large window code-behind files.
- Keep capture flow, settings persistence, file naming, theme switching, and editor rendering separate.
- Preserve both light and dark themes for every new screen or control.
- Capture-mode overlays must never trap the user. `Esc` should always cancel capture cleanly and return the app to its prior state rather than leaving the user stuck.
- Favor keyboard-friendly behavior where reasonable:
- `Esc` cancels capture mode and closes transient windows elsewhere
- `Ctrl+S` saves
- `Ctrl+Shift+S` opens Save As
- `Ctrl+C` copies
- `Ctrl+Z` and `Ctrl+Y` undo and redo in the editor

## Commands
- Restore/build: `dotnet build .\csharp\Privateer.Desktop\Privateer.Desktop.csproj`
- Run: `dotnet run --project .\csharp\Privateer.Desktop\Privateer.Desktop.csproj`
- Self-contained publish for installer: `dotnet publish .\csharp\Privateer.Desktop\Privateer.Desktop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\releases\1.0.0\installer\publish`
- Inno Setup script: `installer\Privateer.iss`
- When a running Privateer process is locking the default build output, verify with an alternate output folder:
- `dotnet build .\csharp\Privateer.Desktop\Privateer.Desktop.csproj -o .\artifacts\verification`

## Local CLI Notes
- If NuGet or profile access causes issues in sandboxed runs, prefer repo-local settings:
- `DOTNET_CLI_HOME=<repo>\.dotnet-cli`
- `NUGET_PACKAGES=<repo>\.nuget\packages`
- `APPDATA=<repo>\.appdata`
- The repo-local `NuGet.Config` should remain the default restore config when needed.

## Verification
- At minimum, make sure the C# project builds after changes.
- Prefer tests for pure helper logic when adding coverage, but do not block feature work on UI automation.
