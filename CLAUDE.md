# EvenMoreSkinColors - Claude context

Experimental cosmetics mod for **Super Battle Golf**.

## Current direction

- Adds a new custom skin-tone area to `PlayerCustomizationMenu`.
- Leaves vanilla `skinColorIndex` intact as a fallback and compatibility layer.
- Exact custom color does **not** fit in vanilla save/network state, so the mod carries its own:
  - local persistence in BepInEx config
  - Mirror messages for exact color replication

## Compatibility strategy

- Do not replace or rewrite `Cosmetics.json`.
- Do not overwrite vanilla `PlayerCosmetics.skinColorIndex` semantics.
- Reapply the custom tone as a material-property-block override after vanilla cosmetics updates.
- Keep UI injection additive: new panel under the existing skin-color area instead of replacing it.
- `MoreSkinColors` compatibility is additive: detect it, leave its vanilla swatches alone, and keep our exact-tone panel separate.

## Known limitations in this first cut

- The custom picker is mouse-driven. Vanilla controller navigation remains intact, but the new picker
  panel itself is not yet integrated into controller-selectable traversal.
- Custom color is now persisted per loadout in BepInEx config, not in the game's own cosmetics save.
- Clients in a lobby need the mod installed to see exact custom colors; otherwise they fall back to
  the synced vanilla `skinColorIndex`.
