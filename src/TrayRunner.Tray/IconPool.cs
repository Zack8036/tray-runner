using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Controls;

namespace TrayRunner.Tray;

public sealed class IconPool : IDisposable
{
    private readonly Bitmap[] _bitmaps;
    private readonly WindowIcon[] _icons;
    private bool _disposed;

    public IconPool(IEnumerable<Uri> frameUris)
    {
        var uriList = frameUris.ToList();
        _bitmaps = new Bitmap[uriList.Count];
        _icons = new WindowIcon[uriList.Count];

        for (int i = 0; i < uriList.Count; i++)
        {
            using var stream = AssetLoader.Open(uriList[i]);
            _bitmaps[i] = new Bitmap(stream);
            _icons[i] = new WindowIcon(_bitmaps[i]);
        }
    }

    public int Count => _icons.Length;

    public WindowIcon this[int index] => _icons[index];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var bmp in _bitmaps)
            bmp.Dispose();
    }
}
