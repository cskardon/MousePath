namespace XClave.MousePath
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public static class Export
    {
        /// <summary>Exports a Canvas to a png file.</summary>
        /// <remarks>
        /// Original code from: http://denisvuyka.wordpress.com/2007/12/03/wpf-diagramming-saving-you-canvas-to-image-xps-document-or-raw-xaml/
        /// </remarks>
        /// <param name="path">The path to save the file to.</param>
        /// <param name="surface">The <see cref="Canvas"/> to save.</param>
        public static void ToPng(Uri path, Canvas surface)
        {
            if (path == null) return;

            // Save current canvas transform
            Transform transform = surface.LayoutTransform;
            
            // reset current transform (in case it is scaled or rotated)
            surface.LayoutTransform = null;

            // Get the size of canvas
            var size = new Size(surface.ActualWidth, surface.ActualHeight);

            // Measure and arrange the surface
            surface.Measure(size);
            surface.Arrange(new Rect(size));

            // Create a render bitmap and push the surface to it
            var renderBitmap =
                new RenderTargetBitmap(
                    (int) size.Width,
                    (int) size.Height,
                    96d,
                    96d,
                    PixelFormats.Pbgra32);
            
            renderBitmap.Render(surface);

            // Create a file stream for saving image
            using (var outStream = new FileStream(path.LocalPath, FileMode.Create))
            {
                // Use png encoder for our data
                var encoder = new PngBitmapEncoder();
                // push the rendered bitmap to it
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                // save the data to the stream
                encoder.Save(outStream);
            }

            // Restore previously saved layout
            surface.LayoutTransform = transform;
        }
    }
}