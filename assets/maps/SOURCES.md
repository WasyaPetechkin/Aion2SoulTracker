# Map data attribution

Kibelisk coordinates: Questlog `map.getMarkers` public dataset, fetched 2026-10-19,
category `location / teleport-artifact`. Included 162 points on the four supported
Elyos/Asmodian maps (61 Verteron, 61 Altgard, 20 Eltnen, 20 Morheim); Abyss points
are excluded. Coordinates use the same convention as the spawn dataset below.
Location names are retained in the source language (English).

The six map textures and `spawns.json` were imported from [xCROv/aion2-nyerk](https://github.com/xCROv/aion2-nyerk), commit `43935db7dc24c6b7a58a0fd9cabb003aa46ae50c` (GPL-3.0; accompanying license: `LICENSE-reference.txt`). Its map dataset records [Questlog's Aion 2 map](https://questlog.gg/aion-2/en/map) as the source and 2026-10-10 as the fetch date. The import converts bundled WebP textures to PNG without changing their pixels. The converted textures and dataset are supplied in the source archive.

Farming metadata in `spawns.json` was refreshed directly from Questlog's public API on
2026-11-06. Soul items (`database.getItems`, category `usable / vehiclesoul`) are matched
to regular pets by their exact English name. `database.getItem.itemIsDroppedByNpcs`
provides the soul-dropping NPC IDs; those IDs are joined to `map.getMarkers` placements.
Dungeon names come from `database.getDungeon` (English and Russian). See
`scripts/import-farming-sources.py` for the transformation of cached API responses.

The six textured maps retain 14,634 coordinate entries for 160 of the 200 regular pets.
The independent `locations` metadata includes 22 areas, including 15 dungeons, and
covers 177 pets; 65 have at least one documented dungeon source. Dungeon metadata
does not imply that a dungeon map texture is included. Entries are possible spawn
locations, not observations of currently living monsters. Variants may share a pet
and occur on more than one territory. No coordinates or missing locations are invented.

For the 23 regular pets without an NPC soul-drop relation, `acquisitionItems` records
the chests listed by their `Pet:` item's `itemIsContainedInItems` relation. These are
pet acquisition sources, not inferred soul drops or proof of exclusive availability.
Chest IDs: 533700068, 533700117–533700120, 533700137. Names use the corresponding
English and Russian `database.getItem` responses.

World boss labels require three pieces of evidence for the same NPC: a soul-drop
relation, a named NPC marker on an open-world map, and `isBoss: true` in the bundled
NPC metadata (see `assets/data/NOTICE.md`). Dungeon-only bosses are excluded.
The matched pet/NPC pairs are 1099/2406071, 1103/2101278, 1156/2100079,
1196/2406035 and 1206/2100718. The label identifies a boss among that pet's sources;
it does not declare every NPC variant of the species a world boss.

Questlog data can include content not yet available on a particular Global EU server.
The metadata is a documented source list, not a claim that all locations were tested
in game or that all dungeon difficulty variants currently award souls.

Map keys and sector bounds come from the same upstream dataset. Spawn coordinates use `(gameX, -gameY)`; image Y increases downwards. The player's coordinates are converted into that convention before projection.

Aion 2 game artwork, names and game data belong to NCSOFT and their respective owners. A software license does not grant ownership of the underlying game artwork or third-party database content.
