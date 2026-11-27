# Conteúdo Tiled

## Mapa

`Maps/Act_01_A.tmx` e `Maps/Act_01_B.tmx` são mapas ortogonais de 320×24 blocos, com tiles de mapa de 16×16 px e viewport lógica de 320×180 px.

Os mapas `Act_02_A.tmx`, `Act_02_B.tmx` e `Act_02_C.tmx` usam o mesmo contrato de 320×24 blocos, mas agora compartilham os tilesets `Act_02_Feudal_Japan.tsx` (dia), `Act_02_Feudal_Japan_Dark.tsx` (noite) e `Act_02_Feudal_Castle.tsx`. A composição visual foi orientada pela referência de paisagem do Japão feudal: céu marfim, montanhas em camadas, arrozais/terraços, telhados de vila, torii, caminhos de santuário e castelo de encosta. Os GIDs e as camadas de colisão/gameplay dos ACTs 02 permanecem estáveis para não alterar o fluxo de jogo.

Os mapas contêm:

- camadas visuais `Background`, `VillageBackdrop`, `Terrain` e `Act01Props`;
- camada de objetos `Collision`, com chão e plataformas;
- camada de objetos `Gameplay`, com `PlayerSpawn`, três checkpoints, pickups, inimigos e o encontro final;
- tileset de ambiente feudal `Tilesets/Act_01_Feudal.tsx` no A;
- tileset feudal japonês dark `Tilesets/Act_01_Feudal_Dark.tsx` no B;
- os três tilesets de personagens vinculados como tilesets externos.

O ambiente usa uma paleta limitada e formas sem suavização para manter o aspecto 16-bit: vila desbotada no A e templo feudal japonês escurecido no B. O A possui a saída para o B; o B reutiliza `Black_Werewolf` e `Skeleton_Warrior` e encerra com uma variante pré-definida de Minotaur.

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

Para o Act 02, os tilesets de animação ativos são `Red_Werewolf`, `Gorgon_2`, `Kitsune` e `Skeleton_Archer`. A geração usa uma lista explícita de estados para evitar incluir variações auxiliares: `Dead` e `Hurt` são não-looping; `Attack_3` da Kitsune contém o lançamento visual do fogo; `Shot_1` e `Shot_2` do Skeleton_Archer contêm a sequência visual do disparo.

`Black_Werewolf` e `Skeleton_Warrior` continuam sendo os inimigos regulares de Act_01_A e Act_01_B.
`Minotaur`, `Gorgon` e `Wizard` são representados por `BossInstance`, com variantes selecionáveis
por `BossVariant`; `Act_01_B` usa `Minotaur_2` no encontro final.
