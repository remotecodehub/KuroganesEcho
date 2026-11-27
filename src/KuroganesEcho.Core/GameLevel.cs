using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Content;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Rendering;
using MonoGame.Extended.Tilemaps.Tiled;

namespace KuroganesEcho.Core;

internal enum GameDifficulty
{
    Easy,
    Normal,
    Hard
}

internal enum CollectibleKind
{
    Health,
    Stamina,
    PowerFruit,
    ExtraLife,
    SealRubyPart,
    // Compatibility with maps created before the ruby reward contract.
    IronTalismanThird,
    SealAshesPart
}

internal sealed class LevelDefinition
{
    public required string Id { get; init; }
    public required string MapAssetPath { get; init; }
    public string CollisionObjectLayerName { get; init; } = "Collision";
    public string GameplayObjectLayerName { get; init; } = "Gameplay";
    public Vector2 PlayerSpawn { get; init; }
    public string NextLevelId { get; init; }
    public required string BriefingTitleKey { get; init; }
    public required string BriefingObjectiveKey { get; init; }
    public required string BriefingTutorialKey { get; init; }

    public static LevelDefinition Act01A => new()
    {
        Id = "Act_01_A",
        MapAssetPath = "Content/Tiled/Maps/Act_01_A.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(64f, 352f),
        NextLevelId = "Act_01_B",
        BriefingTitleKey = "Act01ATitle",
        BriefingObjectiveKey = "Act01AObjective",
        BriefingTutorialKey = "Act01ATutorial"
    };

    public static LevelDefinition Act01B => new()
    {
        Id = "Act_01_B",
        MapAssetPath = "Content/Tiled/Maps/Act_01_B.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(64f, 352f),
        NextLevelId = "Act_01_C",
        BriefingTitleKey = "Act01BTitle",
        BriefingObjectiveKey = "Act01BObjective",
        BriefingTutorialKey = "Act01BTutorial"
    };

    public static LevelDefinition Act01C => new()
    {
        Id = "Act_01_C",
        MapAssetPath = "Content/Tiled/Maps/Act_01_C.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(64f, 352f),
        NextLevelId = null,
        BriefingTitleKey = "Act01CTitle",
        BriefingObjectiveKey = "Act01CObjective",
        BriefingTutorialKey = "Act01CTutorial"
    };

    public static LevelDefinition Act02A => new()
    {
        Id = "Act_02_A",
        MapAssetPath = "Content/Tiled/Maps/Act_02_A.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(64f, 352f),
        NextLevelId = "Act_02_B",
        BriefingTitleKey = "Act02ATitle",
        BriefingObjectiveKey = "Act02AObjective",
        BriefingTutorialKey = "Act02ATutorial"
    };

    public static LevelDefinition Act02B => new()
    {
        Id = "Act_02_B",
        MapAssetPath = "Content/Tiled/Maps/Act_02_B.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(64f, 352f),
        NextLevelId = "Act_02_C",
        BriefingTitleKey = "Act02BTitle",
        BriefingObjectiveKey = "Act02BObjective",
        BriefingTutorialKey = "Act02BTutorial"
    };

    public static LevelDefinition Act02C => new()
    {
        Id = "Act_02_C",
        MapAssetPath = "Content/Tiled/Maps/Act_02_C.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(64f, 352f),
        NextLevelId = null,
        BriefingTitleKey = "Act02CTitle",
        BriefingObjectiveKey = "Act02CObjective",
        BriefingTutorialKey = "Act02CTutorial"
    };

    public static LevelDefinition Act03A => new()
    {
        Id = "Act_03_A",
        MapAssetPath = "Content/Tiled/Maps/Act_03_A.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(32f, 320f),
        NextLevelId = "Act_03_B",
        BriefingTitleKey = "Act03ATitle",
        BriefingObjectiveKey = "Act03AObjective",
        BriefingTutorialKey = "Act03ATutorial"
    };

    public static LevelDefinition Act03B => new()
    {
        Id = "Act_03_B",
        MapAssetPath = "Content/Tiled/Maps/Act_03_B.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(32f, 320f),
        NextLevelId = "Act_03_C",
        BriefingTitleKey = "Act03BTitle",
        BriefingObjectiveKey = "Act03BObjective",
        BriefingTutorialKey = "Act03BTutorial"
    };

    public static LevelDefinition Act03C => new()
    {
        Id = "Act_03_C",
        MapAssetPath = "Content/Tiled/Maps/Act_03_C.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(32f, 320f),
        NextLevelId = null,
        BriefingTitleKey = "Act03CTitle",
        BriefingObjectiveKey = "Act03CObjective",
        BriefingTutorialKey = "Act03CTutorial"
    };

    public static LevelDefinition ForId(string id) => id switch
    {
        "Act_03_C" => Act03C,
        "Act_03_B" => Act03B,
        "Act_03_A" => Act03A,
        "Act_02_C" => Act02C,
        "Act_02_B" => Act02B,
        "Act_02_A" => Act02A,
        "Act_01_C" => Act01C,
        "Act_01_B" => Act01B,
        _ => Act01A
    };
}

internal sealed class LevelObjectData
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required Rectangle Bounds { get; init; }
    public required IReadOnlyDictionary<string, string> Properties { get; init; }
}

internal sealed class GameLevel
{
    private const float PlayerCollisionHalfWidth = 10f;
    private const uint DefaultCollectibleTileFirstGid = 73;
    private const uint TiledGidMask = 0x1FFFFFFF;
    private readonly List<Rectangle> _collisionPlatforms = new();
    private readonly List<FootstepSurface> _collisionSurfaces = new();
    private readonly List<LevelObjectData> _gameplayObjects = new();
    private readonly List<Rectangle> _bellHitboxes = new();

    private GameLevel(LevelDefinition definition, Tilemap tilemap, TilemapSpriteBatchRenderer renderer)
    {
        Definition = definition;
        Tilemap = tilemap;
        Renderer = renderer;
        _playerSpawn = definition.PlayerSpawn;
    }

    private Vector2 _playerSpawn;

    public LevelDefinition Definition { get; }
    public Tilemap Tilemap { get; }
    public TilemapSpriteBatchRenderer Renderer { get; }
    public RectangleF WorldBounds => Tilemap.WorldBounds;
    public Vector2 PlayerSpawn => _playerSpawn;
    public string NextLevelId
    {
        get
        {
            foreach (var exit in LevelExits)
            {
                if (exit.Properties.TryGetValue("NextLevelId", out var nextLevelId) &&
                    !string.IsNullOrWhiteSpace(nextLevelId))
                    return nextLevelId;
            }

            return Definition.NextLevelId;
        }
    }
    public IReadOnlyDictionary<string, string> MapProperties { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public GameDifficulty Difficulty { get; private set; } = GameDifficulty.Normal;
    public IReadOnlyList<Rectangle> CollisionPlatforms => _collisionPlatforms;
    public IReadOnlyList<Rectangle> BellHitboxes => _bellHitboxes;
    public IReadOnlyList<LevelObjectData> GameplayObjects => _gameplayObjects;
    public IEnumerable<LevelObjectData> Bells => _gameplayObjects.Where(item => item.Type == "Bell");
    public IEnumerable<LevelObjectData> Collectibles => _gameplayObjects.Where(item => item.Type == "Collectible");
    public IEnumerable<LevelObjectData> Checkpoints => _gameplayObjects.Where(item => item.Type == "Checkpoint");
    public IEnumerable<LevelObjectData> EnemySpawns => _gameplayObjects.Where(item => item.Type == "EnemySpawn");
    public IEnumerable<LevelObjectData> BossSpawns => _gameplayObjects.Where(item => item.Type == "BossSpawn");
    public IEnumerable<LevelObjectData> LevelExits => _gameplayObjects.Where(item => item.Type == "LevelExit");

    public static GameLevel Load(GraphicsDevice graphicsDevice, LevelDefinition definition)
    {
        var mapDirectory = Path.GetDirectoryName(definition.MapAssetPath)?.Replace('\\', '/') ?? "Content/Tiled/Maps";
        var tiledResourceDirectory = Path.Combine(mapDirectory, "..", "Tilesets").Replace('\\', '/');
        using var mapStream = TitleContainer.OpenStream(definition.MapAssetPath);
        var parser = new TiledTmxParser(tiledResourceDirectory, OpenTiledResource);
        var tilemap = parser.ParseFromStream(mapStream, graphicsDevice, tiledResourceDirectory);
        var renderer = new TilemapSpriteBatchRenderer
        {
            BlendState = BlendState.AlphaBlend,
            SamplerState = SamplerState.PointClamp
        };
        renderer.LoadTilemap(tilemap);

        var level = new GameLevel(definition, tilemap, renderer);
        level.LoadObjects();
        return level;
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch, OrthographicCamera camera)
    {
        Renderer.Update(gameTime);
        Renderer.Draw(spriteBatch, camera);
    }

    public void ConfigureDifficulty(GameDifficulty difficulty) => Difficulty = difficulty;

    public float? FindLandingTop(
        float playerX,
        float previousY,
        float currentY,
        float playerHalfWidth = PlayerCollisionHalfWidth,
        Rectangle? ignoredPlatform = null)
    {
        float? landingTop = null;
        foreach (var platform in _collisionPlatforms)
        {
            if (ignoredPlatform.HasValue && platform == ignoredPlatform.Value)
                continue;
            if (playerX + playerHalfWidth <= platform.Left || playerX - playerHalfWidth >= platform.Right)
                continue;

            var top = platform.Top;
            if (previousY <= top && currentY >= top && (!landingTop.HasValue || top < landingTop.Value))
                landingTop = top;
        }

        return landingTop;
    }

    public Rectangle? FindDropThroughPlatform(float playerX, float feetY, float playerHalfWidth = PlayerCollisionHalfWidth)
    {
        for (var index = 0; index < _collisionPlatforms.Count; index++)
        {
            var platform = _collisionPlatforms[index];
            if (_collisionSurfaces[index] == FootstepSurface.Snow ||
                playerX + playerHalfWidth <= platform.Left ||
                playerX - playerHalfWidth >= platform.Right ||
                Math.Abs(platform.Top - feetY) > 2f)
                continue;

            return platform;
        }

        return null;
    }

    /// <summary>Piso sob os pés do jogador, a partir do nome do objeto de colisão.</summary>
    public FootstepSurface GetSurfaceAt(float playerX, float feetY)
    {
        for (var i = 0; i < _collisionPlatforms.Count; i++)
        {
            var platform = _collisionPlatforms[i];
            if (playerX < platform.Left || playerX > platform.Right)
                continue;

            if (Math.Abs(platform.Top - feetY) <= 2f)
                return _collisionSurfaces[i];
        }

        return FootstepSurface.Snow;
    }

    private static FootstepSurface SurfaceFromName(string name)
    {
        if (name is null)
            return FootstepSurface.Snow;
        if (name.StartsWith("HouseRoof", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("ConstructionRoof", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("CastleRoof", StringComparison.OrdinalIgnoreCase))
            return FootstepSurface.Roof;
        if (name.StartsWith("Platform", StringComparison.OrdinalIgnoreCase))
            return FootstepSurface.Wood;
        return FootstepSurface.Snow;
    }

    private void LoadObjects()
    {
        using var mapStream = TitleContainer.OpenStream(Definition.MapAssetPath);
        var document = XDocument.Load(mapStream);
        var root = document.Root;
        if (root is null)
            return;

        MapProperties = ReadProperties(root);

        var collisionLayer = root.Elements("objectgroup")
            .FirstOrDefault(layer => string.Equals(
                (string)layer.Attribute("name"),
                Definition.CollisionObjectLayerName,
                StringComparison.OrdinalIgnoreCase));
        if (collisionLayer is not null)
        {
            foreach (var objectElement in collisionLayer.Elements("object"))
            {
                var bounds = ReadBounds(objectElement);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    _collisionPlatforms.Add(bounds);
                    _collisionSurfaces.Add(SurfaceFromName((string)objectElement.Attribute("name")));
                }
            }
        }

        var gameplayLayer = root.Elements("objectgroup")
            .FirstOrDefault(layer => string.Equals(
                (string)layer.Attribute("name"),
                Definition.GameplayObjectLayerName,
                StringComparison.OrdinalIgnoreCase));
        if (gameplayLayer is not null)
        {
            foreach (var objectElement in gameplayLayer.Elements("object"))
            {
                var gameplayObject = new LevelObjectData
                {
                    Name = (string)objectElement.Attribute("name") ?? string.Empty,
                    Type = (string)objectElement.Attribute("type") ?? string.Empty,
                    Bounds = ReadBounds(objectElement),
                    Properties = ReadProperties(objectElement)
                };
                _gameplayObjects.Add(gameplayObject);

                if (string.Equals(gameplayObject.Name, "PlayerSpawn", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gameplayObject.Type, "PlayerSpawn", StringComparison.OrdinalIgnoreCase))
                    _playerSpawn = new Vector2(gameplayObject.Bounds.X, gameplayObject.Bounds.Bottom);
            }
        }

        LoadTileBellHitboxes(root);
        LoadTileCollectibles(root);
    }

    private void LoadTileBellHitboxes(XElement root)
    {
        var bellGids = new HashSet<uint>();
        foreach (var bell in Bells)
        {
            if (bell.Bounds.Width > 0 && bell.Bounds.Height > 0)
                _bellHitboxes.Add(bell.Bounds);
        }

        var mapDirectory = Path.GetDirectoryName(Definition.MapAssetPath)?.Replace('\\', '/') ?? "Content/Tiled/Maps";
        foreach (var tileset in root.Elements("tileset"))
        {
            if (!uint.TryParse(
                    (string)tileset.Attribute("firstgid"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var firstGid))
                continue;

            XElement tilesetRoot;
            var source = (string)tileset.Attribute("source");
            if (string.IsNullOrWhiteSpace(source))
            {
                tilesetRoot = tileset;
            }
            else
            {
                var tilesetPath = Path.Combine(mapDirectory, source).Replace('\\', '/');
                using var tilesetStream = OpenTiledResource(tilesetPath);
                tilesetRoot = XDocument.Load(tilesetStream).Root
                    ?? throw new InvalidDataException($"Tileset '{tilesetPath}' has no root element.");
            }

            foreach (var tile in tilesetRoot.Elements("tile"))
            {
                var properties = ReadProperties(tile);
                if (!properties.TryGetValue("TileRole", out var role) ||
                    !string.Equals(role, "ToriiBell", StringComparison.OrdinalIgnoreCase) ||
                    !uint.TryParse(
                        (string)tile.Attribute("id"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var localId))
                    continue;

                bellGids.Add(firstGid + localId);
            }
        }

        if (bellGids.Count == 0)
            return;

        var mapTileWidth = (int?)root.Attribute("tilewidth") ?? 16;
        var mapTileHeight = (int?)root.Attribute("tileheight") ?? 16;
        foreach (var layer in root.Elements("layer"))
        {
            var width = (int?)layer.Attribute("width") ?? 0;
            var height = (int?)layer.Attribute("height") ?? 0;
            var data = layer.Element("data");
            if (width <= 0 || height <= 0 || data is null)
                continue;

            var layerName = (string)layer.Attribute("name") ?? string.Empty;
            var tileLayer = Tilemap.Layers.TryGetValue(layerName, out var loadedLayer) &&
                            loadedLayer is TilemapTileLayer loadedTileLayer
                ? loadedTileLayer
                : null;
            var tileWidth = tileLayer?.TileWidth ?? mapTileWidth;
            var tileHeight = tileLayer?.TileHeight ?? mapTileHeight;
            var gids = ReadTileValues(data, width * height);
            for (var index = 0; index < gids.Count; index++)
            {
                if (!bellGids.Contains(gids[index] & TiledGidMask))
                    continue;

                _bellHitboxes.Add(new Rectangle(
                    (index % width) * tileWidth,
                    (index / width) * tileHeight,
                    tileWidth,
                    tileHeight));
            }
        }
    }

    public void RemoveCollectibleTile(LevelObjectData collectible)
    {
        if (!Tilemap.Layers.TryGetValue("Collectibles", out var layer) || layer is not TilemapTileLayer tileLayer)
            return;

        var tileX = collectible.Bounds.X / Math.Max(1, tileLayer.TileWidth);
        var tileY = collectible.Bounds.Y / Math.Max(1, tileLayer.TileHeight);
        tileLayer.SetTile(tileX, tileY, null);
    }

    public void RevealCollectibleTile(LevelObjectData collectible)
    {
        if (!Tilemap.Layers.TryGetValue("Collectibles", out var layer) || layer is not TilemapTileLayer tileLayer)
            return;
        if (!collectible.Properties.TryGetValue("TileGid", out var gidText) || !uint.TryParse(gidText, out var gid))
            return;

        var tileX = collectible.Bounds.X / Math.Max(1, tileLayer.TileWidth);
        var tileY = collectible.Bounds.Y / Math.Max(1, tileLayer.TileHeight);
        tileLayer.SetTile(tileX, tileY, new TilemapTile((int)gid, TilemapTileFlipFlags.None));
    }

    private void LoadTileCollectibles(XElement root)
    {
        var layer = root.Elements("layer")
            .FirstOrDefault(item => string.Equals(
                (string)item.Attribute("name"),
                "Collectibles",
                StringComparison.OrdinalIgnoreCase));
        if (layer is null)
            return;

        var width = (int?)layer.Attribute("width") ?? 0;
        var height = (int?)layer.Attribute("height") ?? 0;
        var data = layer.Element("data");
        if (width <= 0 || height <= 0 || data is null)
            return;

        var tileLayer = Tilemap.Layers.TryGetValue("Collectibles", out var loadedLayer) && loadedLayer is TilemapTileLayer loadedTileLayer
            ? loadedTileLayer
            : null;
        var tileWidth = tileLayer?.TileWidth ?? (int?)root.Attribute("tilewidth") ?? 16;
        var tileHeight = tileLayer?.TileHeight ?? (int?)root.Attribute("tileheight") ?? 16;
        var collectibleTileset = root.Elements("tileset")
            .FirstOrDefault(item => ((string)item.Attribute("source"))?.EndsWith(
                "Act_01_Items.tsx",
                StringComparison.OrdinalIgnoreCase) == true);
        var collectibleFirstGid = uint.TryParse(
            (string)collectibleTileset?.Attribute("firstgid"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var configuredFirstGid)
            ? configuredFirstGid
            : DefaultCollectibleTileFirstGid;
        var extraLifeTile = ReadTileCoordinate(root, "ExtraLifeTile");
        var powerFruitTile = ReadTileCoordinate(root, "PowerFruitTile");
        var powerFruitTiles = ReadTileCoordinates(root, "PowerFruitTiles");
        if (powerFruitTile.HasValue)
            powerFruitTiles.Add(powerFruitTile.Value);
        var sealRubyPartTiles = ReadTileCoordinates(root, "SealRubyPartTiles");
        if (sealRubyPartTiles.Count == 0)
            sealRubyPartTiles = ReadTileCoordinates(root, "SealAshesPartTiles");
        var finalSealRubyPartTiles = ReadTileCoordinates(root, "FinalSealRubyPartTiles");
        var finalSealPartIndex = ReadIntProperty(root, "FinalSealPartIndex") ?? 4;

        // Act 01 C existed before the ruby metadata was introduced. Accept its
        // old single-tile coordinate so the reward cannot disappear on upgrade.
        var legacyIronTalismanTile = ReadTileCoordinate(root, "IronTalismanTile");
        if (sealRubyPartTiles.Count == 0 && legacyIronTalismanTile.HasValue)
            sealRubyPartTiles.Add(legacyIronTalismanTile.Value);
        var sealPartIndex = ReadIntProperty(root, "SealPartIndex") ?? 1;

        var tileValues = ReadTileValues(data, width * height);
        for (var index = 0; index < tileValues.Count; index++)
        {
            var gid = tileValues[index];
            var tileCoordinate = new Point(index % width, index / width);
            var isPrimarySealTile = sealRubyPartTiles.Contains(tileCoordinate);
            var isFinalSealTile = finalSealRubyPartTiles.Contains(tileCoordinate);
            var staleSealTile = sealRubyPartTiles.Count == 1 &&
                                finalSealRubyPartTiles.Count == 0 &&
                                gid == collectibleFirstGid + 2 &&
                                !isPrimarySealTile;
            var kind = staleSealTile
                ? (CollectibleKind?)null
                : (isPrimarySealTile || isFinalSealTile)
                ? CollectibleKind.SealRubyPart
                : powerFruitTiles.Contains(tileCoordinate)
                    ? CollectibleKind.PowerFruit
                    : gid switch
            {
                _ when gid == collectibleFirstGid => CollectibleKind.Health,
                _ when gid == collectibleFirstGid + 1 => CollectibleKind.Stamina,
                _ when gid == collectibleFirstGid + 2 => CollectibleKind.PowerFruit,
                _ => (CollectibleKind?)null
            };
            if (!kind.HasValue)
                continue;

            var tileX = index % width;
            var tileY = index / width;
            if (kind.Value == CollectibleKind.Health &&
                extraLifeTile.HasValue &&
                extraLifeTile.Value.X == tileX &&
                extraLifeTile.Value.Y == tileY)
                kind = CollectibleKind.ExtraLife;
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PickupKind"] = kind.Value.ToString(),
                ["TileGid"] = gid.ToString(CultureInfo.InvariantCulture)
            };
            if (kind.Value == CollectibleKind.Health)
                properties["RestoreHealth"] = "25";
            else if (kind.Value == CollectibleKind.Stamina)
                properties["RestoreStamina"] = "2.5";
            else if (kind.Value == CollectibleKind.PowerFruit)
                properties["RestorePower"] = CombatBalance.PowerFruitRestore.ToString(CultureInfo.InvariantCulture);
            else if (kind.Value == CollectibleKind.ExtraLife)
                properties["ExtraLife"] = "1";
            else if (kind.Value == CollectibleKind.SealRubyPart)
            {
                var partNumber = isFinalSealTile ? finalSealPartIndex : sealPartIndex;
                properties["UnlockAfterBoss"] = "true";
                properties["SealPartIndex"] = partNumber.ToString(CultureInfo.InvariantCulture);
                if (!isFinalSealTile && sealRubyPartTiles.Count > 1)
                    properties["PickupGroup"] = "SealRubyPart";
            }

            _gameplayObjects.Add(new LevelObjectData
            {
                Name = $"{kind.Value}Tile_{tileX}_{tileY}",
                Type = "Collectible",
                Bounds = new Rectangle(tileX * tileWidth, tileY * tileHeight, tileWidth, tileHeight),
                Properties = properties
            });

            if (kind.Value == CollectibleKind.SealRubyPart)
                tileLayer?.SetTile(tileX, tileY, null);
        }

        // Refactored Act 02 C maps may still contain the four old talisman gids
        // in the layer while only one coordinate is the part released by this
        // boss. Hide those stale visual tiles instead of exposing a full reward.
        if (sealRubyPartTiles.Count == 1 && finalSealRubyPartTiles.Count == 0 && tileLayer is not null)
        {
            for (var index = 0; index < tileValues.Count; index++)
            {
                if (tileValues[index] != collectibleFirstGid + 2)
                    continue;

                var tileCoordinate = new Point(index % width, index / width);
                if (!sealRubyPartTiles.Contains(tileCoordinate))
                    tileLayer.SetTile(tileCoordinate.X, tileCoordinate.Y, null);
            }
        }
    }

    private static int? ReadIntProperty(XElement root, string propertyName)
    {
        var value = root.Element("properties")?.Elements("property")
            .FirstOrDefault(property => string.Equals(
                (string)property.Attribute("name"),
                propertyName,
                StringComparison.OrdinalIgnoreCase))?
            .Attribute("value")?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static Point? ReadTileCoordinate(XElement root, string propertyName)
    {
        var value = root.Element("properties")?.Elements("property")
            .FirstOrDefault(property => string.Equals(
                (string)property.Attribute("name"),
                propertyName,
                StringComparison.OrdinalIgnoreCase))?
            .Attribute("value")?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
               int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) &&
               int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            ? new Point(x, y)
            : null;
    }

    private static List<Point> ReadTileCoordinates(XElement root, string propertyName)
    {
        var value = root.Element("properties")?.Elements("property")
            .FirstOrDefault(property => string.Equals(
                (string)property.Attribute("name"),
                propertyName,
                StringComparison.OrdinalIgnoreCase))?
            .Attribute("value")?.Value;
        var result = new List<Point>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var coordinate in value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = coordinate.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                result.Add(new Point(x, y));
        }

        return result;
    }

    private static List<uint> ReadTileValues(XElement data, int expectedCount)
    {
        var encoding = (string)data.Attribute("encoding");
        if (string.Equals(encoding, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return data.Value
                .Split(new[] { ',', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0u)
                .Take(expectedCount)
                .ToList();
        }

        var encoded = new string(data.Value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        if (encoded.Length == 0)
            return new List<uint>();

        var bytes = Convert.FromBase64String(encoded);
        var count = Math.Min(expectedCount, bytes.Length / 4);
        var values = new List<uint>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = index * 4;
            values.Add((uint)(bytes[offset] |
                              (bytes[offset + 1] << 8) |
                              (bytes[offset + 2] << 16) |
                              (bytes[offset + 3] << 24)));
        }

        return values;
    }

    private static Rectangle ReadBounds(XElement element)
    {
        return new Rectangle(
            (int)ReadFloat(element, "x"),
            (int)ReadFloat(element, "y"),
            (int)ReadFloat(element, "width"),
            (int)ReadFloat(element, "height"));
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(XElement element)
    {
        return element.Element("properties")?
            .Elements("property")
            .Where(property => property.Attribute("name") is not null)
            .ToDictionary(
                property => (string)property.Attribute("name")!,
                property => (string)property.Attribute("value") ?? property.Value,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static float ReadFloat(XElement element, string attributeName) =>
        float.TryParse(
            (string)element.Attribute(attributeName),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0f;

    private static Stream OpenTiledResource(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
            normalized = "Content/" + normalized.TrimStart('/');

        var pathSegments = new List<string>();
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (pathSegments.Count > 1)
                    pathSegments.RemoveAt(pathSegments.Count - 1);
                continue;
            }

            pathSegments.Add(segment);
        }

        normalized = string.Join('/', pathSegments);
        return TitleContainer.OpenStream(normalized);
    }
}
