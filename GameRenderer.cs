using System.Reflection;
using Silk.NET.Maths;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TheAdventure.Models;
using Point = Silk.NET.SDL.Point;

namespace TheAdventure;

public unsafe class GameRenderer
{
    private Sdl _sdl;
    private Renderer* _renderer;
    private GameWindow _window;
    private Camera _camera;

    private Dictionary<int, IntPtr> _texturePointers = new();
    private Dictionary<int, TextureData> _textureData = new();
    private int _textureId;

    public GameRenderer(Sdl sdl, GameWindow window)
    {
        _sdl = sdl;
        _window = window;

        _renderer = (Renderer*)window.CreateRenderer();
        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);

        var windowSize = window.Size;
        _camera = new Camera(windowSize.Width, windowSize.Height);
    }

    public int Width
    {
        get
        {
            Span<int> w = stackalloc int[1];
            Span<int> h = stackalloc int[1];
            _sdl.GetWindowSize(_window.SdlWindow, w, h);
            return w[0];
        }
    }

    public int Height
    {
        get
        {
            Span<int> w = stackalloc int[1];
            Span<int> h = stackalloc int[1];
            _sdl.GetWindowSize(_window.SdlWindow, w, h);
            return h[0];
        }
    }

    public void SetWorldBounds(Rectangle<int> bounds) => _camera.SetWorldBounds(bounds);

    public void CameraLookAt(int x, int y) => _camera.LookAt(x, y);

    public int LoadTexture(string fileName, out TextureData textureInfo)
    {
        using var fStream = new FileStream(fileName, FileMode.Open);
        var image = Image.Load<Rgba32>(fStream);
        textureInfo = new TextureData
        {
            Width = image.Width,
            Height = image.Height
        };
        var raw = new byte[textureInfo.Width * textureInfo.Height * 4];
        image.CopyPixelDataTo(raw);
        fixed (byte* data = raw)
        {
            var surface = _sdl.CreateRGBSurfaceWithFormatFrom(data, textureInfo.Width, textureInfo.Height,
                8, textureInfo.Width * 4, (uint)PixelFormatEnum.Rgba32);
            if (surface == null) throw new Exception("Failed to create surface.");

            var texture = _sdl.CreateTextureFromSurface(_renderer, surface);
            _sdl.FreeSurface(surface);
            if (texture == null) throw new Exception("Failed to create texture.");

            _texturePointers[_textureId] = (IntPtr)texture;
            _textureData[_textureId] = textureInfo;
        }
        return _textureId++;
    }

    public void RenderTexture(int textureId, Rectangle<int> src, Rectangle<int> dst,
        RendererFlip flip = RendererFlip.None, double angle = 0.0, Point center = default, bool useCamera = true)
    {
        if (_texturePointers.TryGetValue(textureId, out var texture))
        {
            var screenDst = useCamera ? _camera.ToScreenCoordinates(dst) : dst;
            _sdl.RenderCopyEx(_renderer, (Texture*)texture, in src, in screenDst, angle, in center, flip);
        }
    }

    public Vector2D<int> ToWorldCoordinates(int x, int y) => _camera.ToWorldCoordinates(new Vector2D<int>(x, y));

    public void SetDrawColor(byte r, byte g, byte b, byte a) => _sdl.SetRenderDrawColor(_renderer, r, g, b, a);

    public void ClearScreen() => _sdl.RenderClear(_renderer);

    public void PresentFrame() => _sdl.RenderPresent(_renderer);

    public void RenderRect(int x, int y, int width, int height, bool useCamera = true)
    {
        var rect = new Rectangle<int>(x, y, width, height);
        if (useCamera) rect = _camera.ToScreenCoordinates(rect);
        _sdl.RenderFillRect(_renderer, in rect);
    }

    public void RenderText(string text, int x, int y, int size = 16)
    {
        SetDrawColor(255, 0, 0, 255);
        RenderRect(x, y, 8 * text.Length, size, false);
    }
}
