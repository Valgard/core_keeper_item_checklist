# ItemChecklist Gotchas

Non-obvious traps that have caused real bugs in this codebase. Read these
before changing UI, prefabs, or layer assignments.

## UI / Scroll

### SetScrollValue(0f) = BOTTOM, not top

`UIScrollWindow.SetScrollValue(0f)` scrolls to the **bottom** of the list,
not the top. Iter-3 passed `0f` and rows overlapped the title element (content
shifted ~20 units up).

**Correct:** `scrollWindow.SetScrollValue(1f)` or `scrollWindow.ResetScroll()`
for top-of-list. Never pass `0f` unless you specifically want the bottom.

See `docs/architecture.md § SetScrollValue Semantics` for the lerp math
explanation.

### Window-open guards must check `root.activeSelf`, not `gameObject.activeSelf`

In a CoreLib `IModUI` window, the `Window`/`UIelement` component sits on the
**parent** GameObject, which CoreLib keeps permanently active. Visibility is
carried by the `root` **child** — `HideUI` toggles `root.SetActive(false)`,
not the parent.

Therefore any guard that means "only do this while the window is open" must
check `root.activeSelf`, **not** `gameObject.activeSelf`:

```csharp
if (!root.activeSelf) return;   // correct — root carries visibility
// if (!gameObject.activeSelf)  // WRONG — parent is always active, never gates
```

Gating on `gameObject.activeSelf` silently never fires (the parent is
always-true), so the guarded code runs even while the window is hidden. This
bit a per-frame recycle guard in Iter-3.8.

### UiController / VirtualScrollList are deleted — do not recreate

`VirtualScrollList.cs`, `UiController.cs`, and `ItemRowView.cs` were
permanently deleted in Iter-2. They were replaced by CK's native
`UIScrollWindow` + `IModUI` pattern + `ItemRow.cs`.

Do not recreate — the old uGUI-based recycler is structurally incompatible
with CK's `Physics.Raycast`-based `UIMouse`. Any Canvas-derived component is
invisible to CK's input system. See § uGUI structurally fails in CK below.

### uGUI (Canvas/Image) structurally fails in CK

CK's `UIMouse` does a `Physics.Raycast` in the UI layer. `Canvas`/`Image`
components have no `Collider` and are invisible to that raycast: input passes
straight through and the cursor stays under the window regardless of Canvas
Render Mode. This is not a configuration problem — it is a structural
incompatibility. All 10 surveyed CK UI mods use `SpriteRenderer` + Layer 5 +
`UIelement`; **0** use uGUI. Do not attempt Canvas-based UI.

## Mod Loading

### Opening the in-game Mods menu wipes the fake-ID dev install

If the in-game **Mods menu** is opened while a fake-ID local dev build is
installed, the mod.io client syncs subscriptions against the real catalog,
finds no entry for the fake ID, and **deletes the local files + ZIP**. The
game must then be restarted without the mod.

**Safe actions:** game start, world load, gameplay — none of these trigger
the sync. **Only the Mods menu** triggers it.

**Recovery:** re-run the install script:
```bash
source .envrc && ../utils/build.sh
```
This rebuilds and re-installs all three fake-ID locations.

**Two-step scenario** (subscribing to a real mod on mod.io): the new
subscription lands only when the Mods menu is opened — the same sync that
applies the subscription wipes every fake-ID mod. Plan for it as a two-step:
open the menu, let the change land, then rebuild each fake-ID mod.

See the parent `CLAUDE.md § Fake-ID dev install` for the full fake-ID
mechanism.

## SpriteMask Clipping

Clipping in CK `SpriteRenderer` UI uses a `SpriteMask` with a **Custom
Sorting-Layer Range**. This section gives the working recipe (Iter-3.5c) first,
then the aborted Iter-3.5b lessons that led to it.

### The working recipe (Iter-3.5c)

- **Sorting layer:** `"GUI"` (uniqueID `1241602095` — verify against
  `CoreKeeperModSDK/ProjectSettings/TagManager.asset` before hardcoding).
- **Custom Range:** `FrontOrder = 55`, `BackOrder = 40`. All row renderers
  must have their `sortingOrder` within this range.
- **IB reference orders:** Background=45, Icon=48, Label=49, Placeholder=49,
  Checkmark=50. Row renderers sit between Background and the mask front-order.
- **`mask_sprite.png`:** 1×1 white PNG. **Must** set `spritePixelsToUnits: 1`
  in the `.meta` (NOT the SDK default of 16 — at PPU=16 the sprite is 0.0625
  units and Transform scale produces a tiny mask instead of full window
  coverage).
- **Mask geometry:** place the SpriteMask as a child of RowsContainer. If
  RowsContainer has a Y offset (e.g. `localPosition.y = 1.5`), the mask needs
  the inverse Y offset (`-1.5`) to stay centered on the background.
- **PugText clipping:** `PugText` has no public `SetSortingLayer` setter. Write
  `style.sortingLayer = 1241602095` directly (`PugText.style` is a public
  field). Prefab YAML keys for PugText are `sortingLayer:` / `orderInLayer:`
  (NOT `m_SortingLayer` / `m_SortingOrder` — those are SpriteRenderer YAML keys).
- **Layer pre-condition:** a mask with Custom Range `40..55` only clips
  renderers already in the `"GUI"` sorting layer. If any SpriteRenderer is
  still in `"Default"`, the mask clips nothing for that renderer. Prefab-edit
  ALL renderers to `"GUI"` before installing the mask.

### Aborted Iter-3.5b lessons

The Iter-3.5b iteration was aborted after pre-flight discovered the
following structural blockers. Documenting them prevents re-attempts.

### "UI" sorting layer does not exist — the named layer is "GUI"

There is **no** named sorting layer `"UI"` in
`CoreKeeperModSDK/ProjectSettings/TagManager.asset`. The sorting layer used
by CK UI elements is named `"GUI"` (uniqueID `1241602095`).

Layer 5 in Unity's tag-layer system is called `"UI"`, but that is a
**tag-layer** (used for `Physics.Raycast` filtering), not a sorting layer.
`"GUI"` (sorting layer) and Layer 5 (tag-layer) are entirely separate
concepts.

Iter-3.5b was designed assuming a `"UI"` sorting layer and was aborted when
Task 1+2 pre-flight revealed the layer does not exist. Always verify sorting
layer uniqueIDs against `TagManager.asset` before hardcoding them into prefab
YAML.

### Pure-runtime SpriteMask cannot cover a mixed Default/GUI renderer stack

A `SpriteMask` with a Custom Sorting-Layer Range of `40..55` only clips
renderers that are **already in that sorting layer**. If any `SpriteRenderer`
is in `"Default"` (order 0) and `PugText`s resolve to `"GUI"` (sentinel
`int.MinValue`), a mask set to GUI range `40..55` clips nothing in `"Default"`.

**Solution (Iter-3.5c approach):** prefab-edit ALL renderers — both
`SpriteRenderer` components and `PugText.style.sortingLayer` fields — to
layer `"GUI"` with `orderInLayer` values within `40..55` **before** installing
the mask. A pure-runtime approach cannot bypass this requirement.

PugText YAML grep pattern: `sortingLayer:` / `orderInLayer:` (NOT
`m_SortingLayer` / `m_SortingOrder` — those are SpriteRenderer YAML keys).

### mask_sprite.png must use spritePixelsToUnits: 1, not 16

The `SpriteMask` sprite (`mask_sprite.png`, a 1×1 white PNG) **must** have
`spritePixelsToUnits: 1` in its `.meta` file. With the SDK default `PPU=16`,
the sprite geometry is `0.0625` units. Applying a Transform scale of `(11, 6)`
produces a `0.69 × 0.375` unit mask instead of the intended `11 × 6` window
coverage — the mask is essentially invisible.

Always set `spritePixelsToUnits: 1` for any mask sprite that needs to cover
a large screen area in CK's `1/16`-unit grid.

### Texture2D + Sprite.Create runtime mask approach was aborted

The Iter-3.5b plan was to generate the mask sprite at runtime via
`new Texture2D(1, 1)` + `Sprite.Create`. This approach was aborted because
the render-domain problem (mixed `"Default"` / `"GUI"` layers) cannot be
solved without prefab edits regardless of how the sprite is created.

Do not revisit this approach without first ensuring all renderers are
consolidated into the same sorting layer. The sprite-creation mechanism is
not the problem — the layer separation is.

### PugText.style has no SetSortingLayer setter — direct field write required

`PugText` has a public `SetOrderInLayer(int)` method but **no public setter
for `sortingLayer`**. To set the sorting layer on a `PugText` at runtime,
write `style.sortingLayer` directly — `PugText.style` is a public field:

```csharp
pugText.style.sortingLayer = 1241602095;  // "GUI" uniqueID
pugText.style.orderInLayer = 48;
```

In prefab YAML, use `sortingLayer:` / `orderInLayer:` keys. Do not use
`m_SortingLayer` / `m_SortingOrder` — those are `SpriteRenderer` YAML keys
and are silently ignored on a `PugText` component.

### PugText tint: set colour after Render(), and keepColorOnStart:true (Iter-6)

`PugText.color`'s setter calls `SetTempColor`, which writes the **glyph
SpriteRenderers** that `Render(text)` (re)builds. Two consequences for tinting
a row label:

1. **Set the colour after `Render()`**, not before — a colour applied before
   `Render()` rebuilds the glyphs is discarded (there are no glyphs yet, or they
   get overwritten).
2. **Use `label.SetTempColor(c, keepColorOnStart: true)`, not `label.color =
   c`.** A prefab `PugText` with `renderOnStart: 1` re-renders once on `Start`
   (one frame after a freshly-instantiated row first activates), resetting the
   glyphs to `style.color` and blanking the tint. With `keepColorOnStart: true`
   the PugText re-applies `tmpColor` on that start-render (`if (_keepColorOnStart)
   SetTempColor(tmpColor)` in the decompile). Symptom of getting this wrong: on
   the **first** open after a world-load the tint appears only after several
   seconds (once a discovery-driven `RefreshVisible` re-binds); subsequent opens
   are fine because the rows have already started.

### Bridge placeholder sprite may be fully transparent → renders nothing (Iter-6)

`ui_rarity_border.png` shipped as an 8×8 PNG with **alpha 0 on every pixel** —
a correct `Sprite` import (`textureType: 8`, `spriteMode: 1`) and present in the
AssetBundle, but invisible. A SpriteRenderer pointed at it draws nothing
regardless of size/order/tint. When a hand-authored sprite "doesn't show",
check the actual pixel alpha (`sips` / PIL) before assuming a wiring/order bug.
The visible placeholder is a white 1-px hollow frame (tinted at runtime by the
rarity colour); `ui_slot_border.png` has the right hollow-frame shape but its
`.meta` is `textureType: 0` (the sprite-meta trap) so it is not usable as a
`Sprite` reference without re-importing.
