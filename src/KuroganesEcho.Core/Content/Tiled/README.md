# Conteúdo Tiled

## Mapa

`Maps/Act_01.tmx` é um mapa ortogonal de 320×24 blocos, com tiles de mapa de 16×16 px e viewport lógica de 320×180 px.

O mapa contém:

- camadas visuais `Background`, `VillageBackdrop`, `Terrain` e `Act01Props`;
- camada de objetos `Collision`, com chão e plataformas;
- camada de objetos `Gameplay`, com `PlayerSpawn`, três checkpoints, pickups, inimigos e o encontro final;
- tileset de ambiente `Tilesets/Act_01_Environment.tsx`;
- os três tilesets de personagens vinculados como tilesets externos.

O ambiente usa uma paleta limitada e formas sem suavização para manter o aspecto 16-bit: vila desbotada, lanternas apagadas, altar sem o selo, névoa, plataformas de madeira e arena do Guardião.

## Personagens

Os tilesets em `Tilesets/` são coleções de imagens:

- `Kunoichi.tsx`: frames de 128×128 px;
- `Ninja_Monk.tsx`: frames de 96×96 px;
- `Ninja_Peasant.tsx`: frames de 96×96 px.

Cada estado cujo PNG é uma spritesheet horizontal possui um tile animado no primeiro frame e os demais frames como tiles de suporte. As propriedades `Character`, `State`, `RenderScale` e `Loop` podem ser lidas pelo carregador MonoGame.

Os frames recortados ficam em `Chars/GeneratedFrames/`. O script `.codex/generate_tiled_char_assets.ps1` pode regenerá-los caso os PNGs originais mudem.

`Spine`, `Kunai` e `Dart` foram mantidos como assets especiais sem animação, pois não são spritesheets de estados de personagem.

## Inimigos e bosses

Os spritesheets em `Enemies/` possuem tilesets animados correspondentes em `Tilesets/Enemies/`.
O script `.codex/generate_tiled_enemy_assets.ps1` recorta os frames e recria esses `.tsx`.

`Black_Werewolf`, `Kitsune` e `Skeleton_Warrior` continuam sendo inimigos regulares.
`Minotaur`, `Gorgon` e `Wizard` são representados por `BossInstance`, com variantes selecionáveis
por `BossVariant`; nenhum boss é instanciado em `Maps/Act_01.tmx`.
