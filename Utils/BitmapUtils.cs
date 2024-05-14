using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Valve.VR;

namespace EasyOpenVR.Utils;

public class BitmapUtils
{
    /// <summary>
    /// Generate the needed bitmap for SteamVR notifications
    /// By default we flip red and blue image channels as that seems to always be required for it to display properly
    /// </summary>
    /// <param name="bmp">The system bitmap</param>
    /// <param name="flipRnB">Whether we should flip red and blue channels or not</param>
    /// <returns></returns>
    public static NotificationBitmap_t NotificationBitmapFromBitmap(Bitmap bmp, bool flipRnB = true)
    {
        return NotificationBitmapFromBitmapData(BitmapDataFromBitmap(bmp, flipRnB));
    }

    public static BitmapData BitmapDataFromBitmap(Bitmap bmpIn, bool flipRnB = false)
    {
        var bmp = (Bitmap)bmpIn.Clone();
        if (flipRnB) RGBtoBGR(bmp);
        var texData = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb
        );
        return texData;
    }

    public static NotificationBitmap_t NotificationBitmapFromBitmapData(BitmapData TextureData)
    {
        var notification_icon = new NotificationBitmap_t
        {
            m_pImageData = TextureData.Scan0,
            m_nWidth = TextureData.Width,
            m_nHeight = TextureData.Height,
            m_nBytesPerPixel = 4
        };
        return notification_icon;
    }

    public static void PointerFromBitmap(Bitmap bmpIn, bool flipRnB, Action<IntPtr> action)
    {
        var bmp = (Bitmap)bmpIn.Clone();
        if (flipRnB) RGBtoBGR(bmp);
        var data = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat
        );
        var pointer = data.Scan0;
        action.Invoke(pointer);
        bmp.UnlockBits(data);
    }

    private static void RGBtoBGR(Bitmap bmp)
    {
        // based on https://docs.microsoft.com/en-us/dotnet/api/system.drawing.bitmap.unlockbits?view=netframework-4.8

        var bytesPerPixel = Bitmap.GetPixelFormatSize(bmp.PixelFormat) / 8;
        var data = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat
        );
        var bytes = Math.Abs(data.Stride) * bmp.Height;

        var ptr = data.Scan0;
        var rgbValues = new byte[bytes];
        Marshal.Copy(data.Scan0, rgbValues, 0, bytes);
        for (var i = 0; i < bytes; i += bytesPerPixel)
        {
            (rgbValues[i], rgbValues[i + 2]) = (rgbValues[i + 2], rgbValues[i]);
        }

        Marshal.Copy(rgbValues, 0, ptr, bytes);
        bmp.UnlockBits(data);
    }
}