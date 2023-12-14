using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using GrapeCity.Documents.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImagesToThumbnail
{
    public class Function
    {
        private const string DestinationBucketName = "imaging-destination"; // Replace with your destination bucket name
        private readonly IAmazonS3 _s3Client = new AmazonS3Client();

        public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            try
            {
                foreach (var record in evnt.Records)
                {
                    var bucketName = record.S3.Bucket.Name;
                    var key = record.S3.Object.Key;

                    // Check if the event is for the original-images bucket
                    if (bucketName != "originall-images")
                    {
                        context.Logger.LogLine($"Ignoring non-original-images bucket: {bucketName}");
                        continue;
                    }

                    var thumbnailKey = "thumbnails/" + key; // Change the path as needed

                    var getObjectRequest = new GetObjectRequest
                    {
                        BucketName = bucketName,
                        Key = key
                    };

                    using (var response = await _s3Client.GetObjectAsync(getObjectRequest))
                    using (var imageStream = response.ResponseStream)
                    {
                        using (var thumbnail = new GcBitmap())
                        {
                            thumbnail.Load(imageStream);
                            thumbnail.Resize(100, 100); // Adjust the thumbnail size as needed

                            // Save thumbnail to S3
                            using (var memoryStream = new MemoryStream())
                            {
                                thumbnail.SaveAsJpeg(memoryStream);
                                memoryStream.Position = 0;

                                var putObjectRequest = new PutObjectRequest
                                {
                                    BucketName = DestinationBucketName,
                                    Key = thumbnailKey,
                                    InputStream = memoryStream,
                                    ContentType = "image/jpeg" // Change content type if needed
                                };

                                await _s3Client.PutObjectAsync(putObjectRequest);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Error: {ex.Message}");
                throw;
            }
        }
    }
}
