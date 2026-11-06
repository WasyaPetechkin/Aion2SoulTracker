"""Build farming metadata from cached Questlog API responses; no network or dependencies.

Usage: python scripts/import-farming-sources.py --cache CACHE --markers MARKERS --date YYYY-MM-DD
Cache contains database.getItem/getDungeon responses (result.data), soul-droppers.json,
and missing-pet-item-sources.json. Markers contains map.getMarkers's full response.
Only explicit soul-item -> NPC -> map relationships are imported. Chest acquisition
is a separate source for the pet, not an inferred soul drop or dungeon placement.
"""
import argparse
import json
from collections import defaultdict
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', required=True, type=Path)
    parser.add_argument('--markers', required=True, type=Path)
    parser.add_argument('--date', required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    path = root / 'assets/maps/spawns.json'
    old = read(path)
    sources = read(args.cache / 'soul-droppers.json')
    catalog = read(root / 'assets/pet-catalog.json')
    regular = {str(p['Id']) for p in catalog if p['Genus'] != 'Special'}
    assert set(sources) == regular, 'Every regular pet must be accounted for'
    npcs = read(root / 'assets/data/npcs/en.json')
    russian = read(root / 'assets/data/npcs/ru.json')
    dungeon_names = read(root / 'assets/data/dungeons/en.json')
    markers = read(args.markers)['result']['data']
    by_npc = defaultdict(list)
    for marker in markers:
        if marker.get('dbType') == 'npc':
            by_npc[str(marker['dbId'])].append(marker)
    open_worlds = {20, 22, 23, 1010, 1011, 1110, 1111}
    areas = {(d['npc'], w['world']): w.get('area') for drops in old['pets'].values()
             for d in drops for w in d['worlds']}
    pets = {}
    used_worlds = set()
    for pet, source in sorted(sources.items()):
        drops = []
        for npc in source['npcs']:
            npc_id = str(npc['id'])
            positions = defaultdict(set)
            world_boss = False
            for marker in by_npc[npc_id]:
                world = int(marker['mapWorldId'])
                for point in marker['coordinates']:
                    assert len(point) >= 2
                    positions[world].add(tuple(point[:2]))
                world_boss |= (world in open_worlds and marker.get('mapCategory') == 'named'
                               and npcs.get(npc_id, {}).get('isBoss') is True)
            used_worlds.update(positions)
            drops.append(dict(npc=int(npc_id), name=npc['name'], nameRu=russian.get(npc_id, {}).get('name'),
                              level=npc.get('level') or 0, chance=npc.get('chance') or 0,
                              worldBoss=world_boss,
                              worlds=[dict(world=w, area=areas.get((int(npc_id), w)), points=sorted(points))
                                      for w, points in sorted(positions.items())]))
        pets[pet] = drops
    names_ru = {20: 'Нижняя Решанта', 22: 'Средняя Решанта', 23: 'Верхняя Решанта',
                1010: 'Вертерон', 1011: 'Элтнен', 1110: 'Альтгард', 1111: 'Морхейм'}
    locations = {}
    for world in sorted(used_worlds):
        if world in open_worlds:
            info = old['worlds'].get(str(world), dict(name='Chaotic Upper Reshanta', group='abyss'))
            locations[str(world)] = dict(name=info['name'], nameRu=names_ru[world], group=info['group'])
        else:
            en = dungeon_names.get(str(world)) or read(args.cache / f'database.getDungeon-{world}-en.json')
            ru = read(args.cache / f'database.getDungeon-{world}-ru.json')
            locations[str(world)] = dict(name=en['name'], nameRu=ru['name'], group='dungeons')
    acquisition = {}
    for pet, _, relations in read(args.cache / 'missing-pet-item-sources.json'):
        assert not sources[str(pet)]['npcs'], 'Do not replace documented NPC drops with chest-only sources'
        acquisition[str(pet)] = [dict(id=int(i['id']), name=i['name'],
            nameRu=read(args.cache / f"database.getItem-{i['id']}-ru.json")['name'])
            for i in relations.get('itemIsContainedInItems', [])]
    assert all(drops or acquisition.get(pet) for pet, drops in pets.items())
    result = dict(source='https://questlog.gg/aion-2/en/map', fetched=args.date,
                  worlds=old['worlds'], locations=locations, acquisitionItems=acquisition, pets=pets)
    path.write_text(json.dumps(result, ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8')
    print(json.dumps(dict(pets=len(pets), locations=len(locations),
        dungeonPets=sum(any(locations[str(w['world'])]['group']=='dungeons' for d in ds for w in d['worlds']) for ds in pets.values()),
        worldBossPets=[p for p, ds in pets.items() if any(d['worldBoss'] for d in ds)],
        chestPets=len(acquisition), mapPoints=sum(len(w['points']) for ds in pets.values() for d in ds for w in d['worlds'] if str(w['world']) in old['worlds'])), indent=2))


if __name__ == '__main__':
    main()
