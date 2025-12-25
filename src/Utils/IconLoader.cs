// Tool Name: Icon Loader
// Description: Loads ribbon and UI icons from the Resources folder next to the DLL.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-25
// Revit Version: 2020
// Dependencies: System.IO, System.Windows.Media.Imaging

using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace AJTools.Utils
{
    /// <summary>
    /// Loads icons from the deployed Resources folder.
    /// </summary>
    internal sealed class IconLoader
    {
        private const int LargeIconSize = 32;
        private const int SmallIconSize = 16;
        private const double TargetDpi = 96.0;
        private readonly string _resourcesFolder;

        /// <summary>
        /// Initializes the icon loader using the add-in assembly path.
        /// </summary>
        /// <param name="assemblyPath">Full path to the executing assembly.</param>
        public IconLoader(string assemblyPath)
        {
            var assemblyFolder = Path.GetDirectoryName(assemblyPath);
            _resourcesFolder = string.IsNullOrWhiteSpace(assemblyFolder)
                ? string.Empty
                : Path.Combine(assemblyFolder, "Resources");
        }

        /// <summary>
        /// Loads a 32x32 ribbon icon.
        /// </summary>
        public BitmapSource LoadLarge(string fileName)
        {
            return Load(fileName, LargeIconSize);
        }

        /// <summary>
        /// Loads a 16x16 ribbon icon.
        /// </summary>
        public BitmapSource LoadSmall(string fileName)
        {
            return Load(fileName, SmallIconSize);
        }

        private BitmapSource Load(string fileName, int decodePixels)
        {
            if (string.IsNullOrWhiteSpace(_resourcesFolder) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var path = Path.Combine(_resourcesFolder, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodePixels;
            bmp.DecodePixelHeight = decodePixels;
            bmp.EndInit();
            bmp.Freeze();
            return NormalizeDpi(bmp);
        }

        private static BitmapSource NormalizeDpi(BitmapSource source)
        {
            if (source == null)
            {
                return null;
            }

            if (Math.Abs(source.DpiX - TargetDpi) < 0.1 && Math.Abs(source.DpiY - TargetDpi) < 0.1)
            {
                return source;
            }

            var pixelWidth = source.PixelWidth;
            var pixelHeight = source.PixelHeight;
            var stride = (pixelWidth * source.Format.BitsPerPixel + 7) / 8;
            var pixels = new byte[pixelHeight * stride];
            source.CopyPixels(pixels, stride, 0);

            var normalized = BitmapSource.Create(
                pixelWidth,
                pixelHeight,
                TargetDpi,
                TargetDpi,
                source.Format,
                source.Palette,
                pixels,
                stride);
            normalized.Freeze();
            return normalized;
        }
    }
}
