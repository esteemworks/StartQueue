using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StartQueue.Core;

namespace StartQueue.UI;

/// <summary>App icons as Explorer shows them, for files and Store apps alike.</summary>
public static class ShellIcons
{
    // Filled from the UI thread and from the background icon loader.
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? For(Entry entry) => ForParsingName(NameFor(entry));

    /// <summary>The shell name whose icon stands for this entry.</summary>
    public static string NameFor(Entry entry) => entry.Kind switch
    {
        EntryKind.PackagedApp => @"shell:AppsFolder\" + entry.Target,
        _ when File.Exists(entry.ResolvedTarget) => entry.ResolvedTarget,
        _ when entry.Target.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) && entry.Arguments.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) => entry.Arguments,
        _ => entry.Target,
    };

    public static bool TryCached(string parsingName, out ImageSource? image) =>
        Cache.TryGetValue(parsingName, out image) || string.IsNullOrWhiteSpace(parsingName);

    public static ImageSource? ForParsingName(string parsingName)
    {
        if (string.IsNullOrWhiteSpace(parsingName)) return null;
        if (Cache.TryGetValue(parsingName, out var cached)) return cached;

        ImageSource? image = null;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var factory) == 0 && factory != null)
            {
                try
                {
                    if (factory.GetImage(new Size32 { Width = 64, Height = 64 }, IconOnly | BiggerSizeOk, out var bitmap) == 0)
                        image = ToBitmapSource(bitmap);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(factory);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"No icon for {parsingName}: {ex.Message}", "icons");
        }

        Cache[parsingName] = image;
        return image;
    }

    static BitmapSource? ToBitmapSource(IntPtr hBitmap)
    {
        try
        {
            var info = new BITMAP();
            if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), ref info) == 0 || info.bmBits == IntPtr.Zero) return null;

            // Shell bitmaps are bottom-up DIB sections with premultiplied alpha.
            var stride = info.bmWidthBytes;
            var source = BitmapSource.Create(info.bmWidth, info.bmHeight, 96, 96, PixelFormats.Pbgra32, null,
                info.bmBits, stride * info.bmHeight, stride);
            var flipped = new TransformedBitmap(source, new ScaleTransform(1, -1));
            var frozen = new WriteableBitmap(flipped);
            frozen.Freeze();
            return frozen;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    const int BiggerSizeOk = 0x1;
    const int IconOnly = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    struct Size32
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(Size32 size, int flags, out IntPtr bitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);

    [DllImport("gdi32.dll")]
    static extern int GetObject(IntPtr handle, int size, ref BITMAP bitmap);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr handle);
}
