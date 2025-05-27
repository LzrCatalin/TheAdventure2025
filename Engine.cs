using System.Reflection;
using System.Text.Json;
using Silk.NET.Maths;
using Silk.NET.SDL;
using TheAdventure.Models;
using TheAdventure.Models.Data;
using TheAdventure.Scripting;

namespace TheAdventure;

public class Engine
{
    private readonly GameRenderer _renderer;
    private readonly Input _input;
    private readonly ScriptEngine _scriptEngine = new();

    private readonly Dictionary<int, GameObject> _gameObjects = new();
    private readonly Dictionary<string, TileSet> _loadedTileSets = new();
    private readonly Dictionary<int, Tile> _tileIdMap = new();

    private Level _currentLevel = new();
    private PlayerObject? _player;

    private DateTimeOffset _lastUpdate = DateTimeOffset.Now;
    
    private int _heartTextureId;
    private TextureData _heartTextureData;
    
    public int GetPlayerLives() => _player?.Lives ?? 0;
    public int GetPlayerScore() => _player?.Score ?? 0;
    
    public Engine(GameRenderer renderer, Input input)
    {
        _renderer = renderer;
        _input = input;

        _input.OnMouseClick += (_, coords) => AddBomb(coords.x, coords.y);
    }

    public void SetupWorld()
    {
        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);
        _player.ResetStats(); // reset stats at the beginning
        
        // Load heart design
        _heartTextureId = _renderer.LoadTexture(Path.Combine("Assets", "Images", "heart-icon.jpg"), out _heartTextureData);

        
        var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));
        var level = JsonSerializer.Deserialize<Level>(levelContent);
        if (level == null)
        {
            throw new Exception("Failed to load level");
        }

        foreach (var tileSetRef in level.TileSets)
        {
            var tileSetContent = File.ReadAllText(Path.Combine("Assets", tileSetRef.Source));
            var tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent);
            if (tileSet == null)
            {
                throw new Exception("Failed to load tile set");
            }

            foreach (var tile in tileSet.Tiles)
            {
                tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                _tileIdMap.Add(tile.Id!.Value, tile);
            }

            _loadedTileSets.Add(tileSet.Name, tileSet);
        }

        if (level.Width == null || level.Height == null)
        {
            throw new Exception("Invalid level dimensions");
        }

        if (level.TileWidth == null || level.TileHeight == null)
        {
            throw new Exception("Invalid tile dimensions");
        }

        _renderer.SetWorldBounds(new Rectangle<int>(0, 0, level.Width.Value * level.TileWidth.Value,
            level.Height.Value * level.TileHeight.Value));

        _currentLevel = level;

        _scriptEngine.LoadAll(Path.Combine("Assets", "Scripts"));
    }

    public void ProcessFrame()
    {
        var currentTime = DateTimeOffset.Now;
        var msSinceLastFrame = (currentTime - _lastUpdate).TotalMilliseconds;
        _lastUpdate = currentTime;

        if (_player == null)
        {
            return;
        }

        double up = _input.IsUpPressed() ? 1.0 : 0.0;
        double down = _input.IsDownPressed() ? 1.0 : 0.0;
        double left = _input.IsLeftPressed() ? 1.0 : 0.0;
        double right = _input.IsRightPressed() ? 1.0 : 0.0;
        bool isAttacking = _input.IsKeyAPressed() && (up + down + left + right <= 1);
        bool addBomb = _input.IsKeyBPressed();

        _player.UpdatePosition(up, down, left, right, 48, 48, msSinceLastFrame);
        if (isAttacking)
        {
            _player.Attack();
        }
        
        _scriptEngine.ExecuteAll(this);

        if (addBomb)
        {
            AddBomb(_player.Position.X, _player.Position.Y, false);
        }
    }
    
    private void RenderHud()
    {
        int heartSize = 24;
        int heartSpacing = 6;
        int hudMargin = 10;

        int scoreBarMaxWidth = 200;
        int scoreBarHeight = 16;

        // Draw hearts
        for (int i = 0; i < _player.Lives; i++)
        {
            var drawRect = new Rectangle<int>(
                hudMargin + i * (heartSize + heartSpacing),
                hudMargin,
                heartSize,
                heartSize
            );

            _renderer.RenderTexture(
                _heartTextureId,
                new Rectangle<int>(0, 0, _heartTextureData.Width, _heartTextureData.Height),
                drawRect,
                RendererFlip.None,
                0.0,
                default,
                false 
            );
        }

        // Score bar drawing
        int scoreY = hudMargin + heartSize + 10;
        int currentScoreWidth = Math.Min(_player.Score, scoreBarMaxWidth);

        // Score bar background gray
        _renderer.SetDrawColor(60, 60, 60, 220);
        _renderer.RenderRect(hudMargin - 2, scoreY - 2, scoreBarMaxWidth + 4, scoreBarHeight + 4, false);

        // Fill of the score bar with yellow
        _renderer.SetDrawColor(255, 215, 0, 255);
        _renderer.RenderRect(hudMargin, scoreY, currentScoreWidth, scoreBarHeight, false);
    }

    
    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        var playerPosition = _player!.Position;
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        RenderTerrain();
        RenderAllObjects();
        
        RenderHud();

        _renderer.PresentFrame();
    }

    public void RenderAllObjects()
    {
        var toRemove = new List<int>();
        foreach (var gameObject in GetRenderables())
        {
            gameObject.Render(_renderer);
            if (gameObject is TemporaryGameObject { IsExpired: true } tempGameObject)
            {
                toRemove.Add(tempGameObject.Id);
            }
        }

        foreach (var id in toRemove)
        {
            _gameObjects.Remove(id, out var gameObject);

            if (_player == null)
            {
                continue;
            }

            var tempGameObject = (TemporaryGameObject)gameObject!;
            var deltaX = Math.Abs(_player.Position.X - tempGameObject.Position.X);
            var deltaY = Math.Abs(_player.Position.Y - tempGameObject.Position.Y);
            // If the player doesnt die, the score will increase, else, game over
            if (deltaX < 32 && deltaY < 32)
            {
                _player.LoseLife();
            }
            else
            {
                _player.AddScore(10); // add 10 for each missed bomb
            }
        }

        _player?.Render(_renderer);
    }
    
    public void RenderTerrain()
    {
        foreach (var currentLayer in _currentLevel.Layers)
        {
            for (int i = 0; i < _currentLevel.Width; ++i)
            {
                for (int j = 0; j < _currentLevel.Height; ++j)
                {
                    int? dataIndex = j * currentLayer.Width + i;
                    if (dataIndex == null)
                    {
                        continue;
                    }

                    var currentTileId = currentLayer.Data[dataIndex.Value] - 1;
                    if (currentTileId == null)
                    {
                        continue;
                    }

                    var currentTile = _tileIdMap[currentTileId.Value];

                    var tileWidth = currentTile.ImageWidth ?? 0;
                    var tileHeight = currentTile.ImageHeight ?? 0;

                    var sourceRect = new Rectangle<int>(0, 0, tileWidth, tileHeight);
                    var destRect = new Rectangle<int>(i * tileWidth, j * tileHeight, tileWidth, tileHeight);
                    _renderer.RenderTexture(currentTile.TextureId, sourceRect, destRect);
                }
            }
        }
    }

    public IEnumerable<RenderableGameObject> GetRenderables()
    {
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is RenderableGameObject renderableGameObject)
            {
                yield return renderableGameObject;
            }
        }
    }

    public (int X, int Y) GetPlayerPosition()
    {
        return _player!.Position;
    }

    public void AddBomb(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);

        SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
        spriteSheet.ActivateAnimation("Explode");

        TemporaryGameObject bomb = new(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(bomb.Id, bomb);
    }
}