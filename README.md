# Ground Items With Linq (fork)

An ExileApi plugin that highlights ground items matching your `.ifl` filter rules.

This fork adds an on-ground name overlay, per-rule labels/colours/frames/sounds, and a
name list for tracking specific uniques.

## Credits

This fork stands on three plugins. All the hard parts — reading the game's item data,
and working out that an unidentified unique can be identified by its art path — were
solved by their authors, not by this fork.

### [DetectiveSquirrel/Ground-Items-With-Linq](https://github.com/DetectiveSquirrel/Ground-Items-With-Linq)

The plugin this is forked from. Everything here is built on it: the filter loading and
rule ordering, the item state tracking, the side panel, socket rendering, the compass,
and the `UniqueArtManager` that resolves an item's art path to its possible unique names.

### [exApiTools/Get-Chaos-Value](https://github.com/exApiTools/Get-Chaos-Value)

Source of two features ported here:

- **The on-ground name overlay.** The technique in `GroundNameOverlay` — opening a
  throwaway ImGui window so `SetWindowFontScale` applies, drawing into the background
  draw list, and picking the text layout that best fits the item's label box — is taken
  from its `ShowRealUniqueNameOnGround`. Adapted to trigger on filter matches rather
  than a poe.ninja price threshold, since this plugin has no price feed.
- **Sound notifications.** The design in `SoundNotifier` — per-item wav files resolved
  by name from the config directory, with a played-tracker so an item alerts once
  rather than every frame, cleared on area change — follows its implementation.

### [Relvl/POE_API_UniqueFinder](https://github.com/Relvl/POE_API_UniqueFinder)

Source of the approach behind the unique highlight list: keep a plain list of unique
names in settings and match it case-insensitively against the art-derived names, rather
than trying to express it as a filter query (which cannot work — see below).

Two things are done differently here: every art candidate is tested rather than only the
first, since art paths routinely map to several uniques; and the settings row reports
which uniques share the name's art as you type. Its careful `Replica` handling is worth
noting as better than this fork's.

## Why unique names are not filter queries

`ItemFilter` compiles each query against `ItemData`. The art-derived `UniqueNameCandidates`
live on `CustomItemData`, so they are out of scope inside a query — and an unidentified
unique has no other name to match on, because `BaseName` is only the base type. Mageblood
on the ground is a `Heavy Belt` and nothing more.

That is why the highlight list exists as its own setting instead of as filter syntax.

Roughly one unique in eight is ambiguous by art (171 of 1274 art paths map to more than
one name), so `Timeclasp` will always also match `Timetwist`. No plugin can do better;
the information does not exist client-side until the item is identified.
