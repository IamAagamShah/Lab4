using System.Drawing;
using GrapeCity.Documents.Drawing;
using GrapeCity.Documents.Text;
using GrapeCity.Documents.Imaging;

namespace ImagesToThumbnail
{
    internal class ImagingOperations
    {
        public static byte[] GetConvertedImage(byte[] imageBytes, int maxWidth, int maxHeight)
        {
            using (var originalImage = new GcBitmap())
            {
                using (var ms = new MemoryStream(imageBytes))
                {
                    originalImage.Load(ms);

                    // Determine the thumbnail size while preserving the aspect ratio.
                    Size newSize = CalculateThumbnailSize(originalImage.Width, originalImage.Height, maxWidth, maxHeight);

                    // Resize the image to the thumbnail size.
                    using (var resizedImage = originalImage.Resize(newSize.Width, newSize.Height, InterpolationMode.Cubic))
                    {
                        using (var resultStream = new MemoryStream())
                        {
                            // Save the resized image to a new memory stream.
                            resizedImage.SaveAsJpeg(resultStream);
                            // Ensure the stream is set to the beginning before reading
                            resultStream.Position = 0;
                            return resultStream.ToArray();
                        }
                    }
                }
            }
        }


        private static Size CalculateThumbnailSize(float originalWidth, float originalHeight, int maxWidth, int maxHeight)
        {
            double widthRatio = (double)maxWidth / originalWidth;
            double heightRatio = (double)maxHeight / originalHeight;
            double ratio = Math.Min(widthRatio, heightRatio);

            int newWidth = (int)(originalWidth * ratio);
            int newHeight = (int)(originalHeight * ratio);

            return new Size(newWidth, newHeight);
        }

    }
}