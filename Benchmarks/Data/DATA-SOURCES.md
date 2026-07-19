# Benchmark Data Sources

Sample files used for FracturedJson performance benchmarks (and potentially unit tests).

See individual entries below for source, processing notes, and licensing information. Attribution is included where required or appropriate.

### `geojson-sm.json` / `geojson-lg.json`

- **Source**: Converted U.S. Census Bureau Cartographic Boundary Files by Eric Celeste.
- **Original data**: U.S. Census Bureau (public domain).
- **Conversion & hosting**: https://eric.clst.org/tech/usgeojson/
- **License**: U.S. Government work — no copyright protection. Free to use for any purpose.
- **Notes**: Simplified cartographic boundaries suitable for testing large-scale GeoJSON formatting and fracturing.
- **Date acquired**: 2026-07-12

### `battleplan.json`

- **Source**: Sample scenario file from my open-source tower defense game *BattlePlan*.
- **Repository**: https://github.com/j-brooke/BattlePlan
- **License**: Open source (see repo for details).
- **Notes**: Chosen for its nested game state structure and realistic JSON texture. Used as-is for benchmark variety.
- **Date acquired**: 2026-07-12

### `pokeapi-sm.json` / `pokeapi-md.json` / `pokeapi-lg.json`

- **Source**: PokeAPI ( https://pokeapi.co/ )
- **License**: Data is provided under a CC0 / public domain equivalent license. Free to use with no attribution required, though the project appreciates credit.
- **Notes**: Varying file sizes of detailed Pokémon entries combined. Good variety of nested objects and repeated structures.
- **Date acquired**: 2026-07-12

### `fjjs-tsconfig.jsonc` (from FracturedJsonJs)

- **Source**: The TypeScript configuration file from my FracturedJsonJs repository ( https://github.com/j-brooke/FracturedJsonJs ).
- **License**: Same as the FracturedJsonJs project (open source — see repo for details).
- **Notes**: Richly commented `tsconfig.json` with many explanatory notes and commented-out options. Excellent for testing JSONC comment handling and preservation.  Renamed to avoid confusion about its purpose in this folder.
- **Date acquired**: 2026-07-12
