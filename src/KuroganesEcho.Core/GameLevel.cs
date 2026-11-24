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

internal sealed class LevelDefinition
{
    public required string Id { get; init; }
    public required string MapAssetPath { get; init; }
    public string CollisionObjectLayerName { get; init; } = "Collision";
    public string GameplayObjectLayerName { get; init; } = "Gameplay";
    public Vector2 PlayerSpawn { get; init; }

    public static LevelDefinition Act01 => new()
    {
        Id = "Act_01",
        MapAssetPath = "Content/Tiled/Maps/Act_01.tmx",
        CollisionObjectLayerName = "Collision",
        GameplayObjectLayerName = "Gameplay",
        PlayerSpawn = new Vector2(64f, 352f)
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
    private readonly List<Rectangle> _collisionPlatforms = new();
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
    public IReadOnlyList<Rectangle> CollisionPlatforms => _collisionPlatforms;
    public IReadOnlyList<LevelObjectData> GameplayObjects => _gameplayObjects;
    public IEnumerable<LevelObjectData> EnemySpawns => _gameplayObjects.Where(item => item.Type == "EnemySpawn");

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
                    _collisionPlatforms.Add(bounds);
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
