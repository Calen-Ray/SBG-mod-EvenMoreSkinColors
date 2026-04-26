# Changelog

## 0.2.0

- Stop kicking vanilla clients out of modded lobbies. The skin-tone state broadcast was using
  Mirror's `SendToAll`, which delivered our custom message id to vanilla peers — Mirror
  disconnects the offending client on unknown messages. The host now tracks which connections
  have proven they speak this protocol (via snapshot or update requests) and fans out only to
  those, leaving vanilla peers untouched.
- Clean up the modded-connections set on disconnect via `NetworkServer.OnDisconnectedEvent`
  so reconnecting clients get re-detected on their next snapshot request.
- Silence the per-event `EMSC_DEBUG` broadcast/receive/apply/revert log lines by default; flip
  `Diagnostics.VerboseLogging = true` to bring them back when reporting an issue.

## 0.1.3

- Remove the redundant "Show/Hide Wheel" toolbar button. The whole-panel minimize from 0.1.2
  already covers the same need without the second confusable affordance, and the wheel is now
  always visible while the panel is expanded.
- Fix wheel-drag stutter. Three changes:
  - Skip the SV gradient regen when hue hasn't moved (it's parameterized only by hue, so
    SV-square drags reuse the existing texture and tiny wheel jitters do too) and switch from
    65,536 `SetPixel` calls to one `SetPixels32` upload using a reused buffer.
  - Defer disk-save and Mirror broadcast to drag-end. During the drag we still apply the new
    color to the preview material live, but `cray.evenmoreskincolors.cfg` now writes once on
    pointer-up via a new `IPointerUpHandler` / `IEndDragHandler` pair on the drag surface, and
    a `_dragFinalizing` guard makes sure the two handlers only commit once between them.
  - Drop the duplicate `ApplyToPreview` at the end of `RefreshUi` — `SetLocalSelection`
    already calls it; the explicit call is kept on the cold paths (`Initialize`, minimize/
    restore) that don't go through `SetLocalSelection`.

## 0.1.2

- Add a whole-panel minimize/restore so the picker no longer covers the character preview and
  the vanilla skin-color buttons. A new `\u2013` button in the panel's top-right collapses
  everything to a small "Custom Tone" launcher pill (with a live swatch); clicking the pill
  expands the picker back. State is persisted in `cray.evenmoreskincolors.cfg` under
  `[Picker.Panel] Minimized`, so the panel reopens in whichever mode you last left it.

## 0.1.1

- Fix the color-wheel picker being unresponsive and appearing locked to the current character tone.
  Three separate problems contributed: the panel was being parented inside the Skincolors
  GridLayoutGroup (which squashed it, injected a `Content` wrapper, and hid AdvancedArea), the
  wheel's pointer math assumed a centered pivot while the box was top-left pinned (so clicks in
  the ring always fell outside the hit distance), and the handle Images were raycast targets
  that could steal events from the drag surface. Panel now parents to the `Preview` container,
  pointer math is pivot-agnostic via `rect.rect.center`, and handles are raycast-inert.

## 0.1.0

- Initial release candidate.
- Adds a custom multiplayer-synced skin tone override with a color-wheel UI in character customization.
- Adds per-loadout persistence in BepInEx config.
- Adds quick presets, hex entry, reset-to-vanilla, and reset-to-default controls.
- Adds additive compatibility handling for MoreSkinColors.
- Adds debug log hooks for harness-driven verification.
