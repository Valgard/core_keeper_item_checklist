# ItemChecklist — Future Roadmap

Frozen 2026-06-04. The backlog of planned iterations (Iter-12 onward).

- **Iter-10 — DONE** (see `docs/iteration-history.md`).
- **Iter-11 — DONE** (see `docs/iteration-history.md`). Note: implemented via
  native TextDataBlock generation + `LocalizationGenerator.cs`, **not** CoreLib
  `LocalizationModule` (which is deprecated).
- **Iter-12 -- real pixel-art sprites.** Replace the placeholder rarity border
  (white 9-slice hollow frame) + scrollbar track/handle sprites.
- **Iter-13 -- `DropdownWidget` prefab extraction.** Extract the widget into a
  standalone/nested prefab for true reuse.
- **Iter-14 -- code refactor / optimisations + search caret vertical alignment.**
  General C# cleanup; plus the search caret sits a few px too low --
  `TextInputField` forces `characterMarkBlinker.transform.position =
  pugText.position` every frame, so the fix is to move the white_pixel into a child
  GO with a +Y offset and rewire `CharacterMarkBlinker.sr`.
- **Iter-15 (tentative) -- F1 guard misses loading screen & cutscenes.** The F1
  toggle in `ItemChecklistMod.Update` only blocks opening when a Vanilla
  menu/inventory/text-field/chat is active; it does **not** block during the world
  loading screen or in-game cutscenes, so F1 pops the checklist over both. Fix =
  add the missing state guards (loading: `ClientWorldStateSystem.HasRunAtLeastOnce`
  / `Manager.main.player != null` or a screen-fade flag; cutscenes: a cutscene /
  input-locked flag -- all need ILSpy + sandbox verification). Bugfix follow-up to
  Iter-4's toggle guard.
- **Iter-16 (tentative) -- pet/creature discovery.** The bake blanket-excludes
  `ObjectType.Creature`/`Critter`, so tamed pets/critters never get a row -- same
  bug class as the Iter-7.1 NonUsable fix. IB keeps anything with `PetCD`
  (`ObjectUtility.cs:390`) and craftable non-cattle creatures (`CraftingCD &&
  !CattleCD`, `:393`); a fix would mirror those, still dropping wild mobs.
  `PugDatabase.HasComponent<T>` is sandbox-safe here. Sibling to Iter-7.1.
- **Iter-17 (tentative) -- per-variation/skin tracking.** The bake collapses every
  family to its `variation == 0` entry (`ItemCatalog.cs:130`), so colour/skin/state
  variants never get their own row. CK tracks discovery per `(objectID, variation)`
  and IB exposes `ignoreVariation` (`ObjectUtility.cs:422`); we hardwired "ignore
  variation" to keep a one-tick-per-item checklist. Revisit only with a UI story
  for grouping/expanding variants. Distinct from the Iter-7.1 catalog fix.

See `git log` for canonical per-iter merge points and `docs/superpowers/specs/`
for design docs.
