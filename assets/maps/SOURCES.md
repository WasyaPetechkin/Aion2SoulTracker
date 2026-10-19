# Map data attribution

The six map textures and `spawns.json` were imported from [xCROv/aion2-nyerk](https://github.com/xCROv/aion2-nyerk), commit `43935db7dc24c6b7a58a0fd9cabb003aa46ae50c` (GPL-3.0; accompanying license: `LICENSE-reference.txt`). Its map dataset records [Questlog's Aion 2 map](https://questlog.gg/aion-2/en/map) as the source and 2026-10-10 as the fetch date. The import converts bundled WebP textures to PNG without changing their pixels. The converted textures and dataset are supplied in the source archive.

The dataset has 14,634 coordinate entries across six maps, with known locations for 160 of the 200 regular pets. Entries are possible spawn locations, not observations of currently living monsters. Some species have multiple NPC variants and locations on both factions' territories. No coordinates are invented for entries without known locations.

Map keys and sector bounds come from the same upstream dataset. Spawn coordinates use `(gameX, -gameY)`; image Y increases downwards. The player's coordinates are converted into that convention before projection.

Aion 2 game artwork, names and game data belong to NCSOFT and their respective owners. A software license does not grant ownership of the underlying game artwork or third-party database content.
