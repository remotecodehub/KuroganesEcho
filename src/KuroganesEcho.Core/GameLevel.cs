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
    IronTalismanThird
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

    public static LevelDefinition ForId(string id) => id switch
    {
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
    private readonly List<Rectangle> _collisionPlatforms = new();
    private readonly List<FootstepSurface> _collisionSurfaces = new();
    private readonly List<LevelObjectData> _gameplayObjects = new();

    private GameLevel(LevelDefinition definition, Tilemap tilemap, TilemapSpriteBatchRenderer renderer)
    {
        Definition = definition;
        Tilemap = tilemap;
        Renderer = renderer;
    }

    public LevelDefinition Definition { get; }
    public Tilemap Tilemap { get; }
    public TilemapSpriteBatchRenderer Renderer { get; }
    public RectangleF WorldBounds => Tilemap.WorldBounds;
    public Vector2 PlayerSpawn => Definition.PlayerSpawn;
    public GameDifficulty Difficulty { get; private set; } = GameDifficulty.Normal;
    public IReadOnlyList<Rectangle> CollisionPlatforms => _collisionPlatforms;
    public IReadOnlyList<LevelObjectData> GameplayObjects => _gameplayObjects;
    public IEnumerable<LevelObjectData> Collectibles => _gameplayObjects.Where(item => item.Type == "Collectible");
    public IEnumerable<LevelObjectData> Checkpoints => _gameplayObjects.Where(item => item.Type == "Checkpoint");
    public IEnumerable<LevelObjectData> EnemySpawns => _gameplayObjects.Where(item => item.Type == "EnemySpawn");
    public IEnumerable<LevelObjectData> BossSpawns => _gameplayObjects.Where(item => item.Type == "BossSpawn");
    public IEnumerable<LevelObjectData> LevelExits => _gameplayObjects.Where(item => item.Type == "LevelExit");

    public static GameLevel Load(GraphicsDevice graphicsDevice, LevelDefinition definition)
    {
        var mapDirectory = Path.GetDirectoryName(definition.MapAssetPath)?.Replace('\\', '/') ?? "Content/Tiled/Maps";
        using var mapStream = TitleContainer.OpenStream(definition.MapAssetPath);
        var parser = new TiledTmxParser(mapDirectory, OpenTiledResource);
        var tilemap = parser.ParseFromStream(mapStream, graphicsDevice, mapDirectory);
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

    public float? FindLandingTop(float playerX, float previousY, float currentY, float playerHalfWidth = PlayerCollisionHalfWidth)
    {
        float? landingTop = null;
        foreach (var platform in _collisionPlatforms)
        {
            if (playerX + playerHalfWidth <= platform.Left || playerX - playerHalfWidth >= platform.Right)
                continue;

            var top = platform.Top;
            if (previousY <= top && currentY >= top && (!landingTop.HasValue || top < landingTop.Value))
                landingTop = top;
        }

        return landingTop;
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
        if (name.StartsWith("HouseRoof", StringComparison.OrdinalIgnoreCase))
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
                _gameplayObjects.Add(new LevelObjectData
                {
                    Name = (string)objectElement.Attribute("name") ?? string.Empty,
                    Type = (string)objectElement.Attribute("type") ?? string.Empty,
                    Bounds = ReadBounds(objectElement),
                    Properties = ReadProperties(objectElement)
                });
            }
        }

        LoadTileCollectibles(root);
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
        var ironTalismanTile = ReadTileCoordinate(root, "IronTalismanTile");

        var tileValues = ReadTileValues(data, width * height);
        for (var index = 0; index < tileValues.Count; index++)
        {
            var gid = tileValues[index];
            var tileCoordinate = new Point(index % width, index / width);
            var kind = ironTalismanTile.HasValue && ironTalismanTile.Value == tileCoordinate
                ? CollectibleKind.IronTalismanThird
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
            else
                properties["UnlockAfterBoss"] = "true";

            _gameplayObjects.Add(new LevelObjectData
            {
                Name = $"{kind.Value}Tile_{tileX}_{tileY}",
                Type = "Collectible",
                Bounds = new Rectangle(tileX * tileWidth, tileY * tileHeight, tileWidth, tileHeight),
                Properties = properties
            });

            if (kind.Value == CollectibleKind.IronTalismanThird)
                tileLayer?.SetTile(tileX, tileY, null);
        }
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

        return TitleContainer.OpenStream(normalized);
    }
}
