using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImagesToThumbnail;

public class Function
{
    IAmazonS3 S3Client { get; set; }

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client"></param>
    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var destBucket = "imaging-destination";
        var s3Event = evnt.Records?[0].S3;
        if (s3Event == null)
        {
            context.Logger.LogLine("No S3 event detected in the Lambda event.");
            return "No S3 event detected in the Lambda event.";
        }

        try
        {
            var metadataResponse = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);

            if (!metadataResponse.Headers.ContentType.StartsWith("image/"))
            {
                context.Logger.LogLine($"The file {s3Event.Object.Key} is not an image.");
                return $"The file {s3Event.Object.Key} is not an image.";
            }

            using (GetObjectResponse response = await S3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key))
            using (Stream responseStream = response.ResponseStream)
            using (var memstream = new MemoryStream())
            {
                await responseStream.CopyToAsync(memstream);
                memstream.Position = 0;

                byte[] thumbnailBytes = ImagingOperations.GetConvertedImage(memstream.ToArray(), 150, 150);
                using (var thumbnailStream = new MemoryStream(thumbnailBytes))
                {
                    PutObjectRequest putRequest = new PutObjectRequest
                    {
                        BucketName = destBucket,
                        Key = $"thumbnail-{s3Event.Object.Key}",
                        InputStream = thumbnailStream,
                        ContentType = "image/jpeg" // Ensure the correct content type is set for the thumbnail
                    };

                    await S3Client.PutObjectAsync(putRequest);
                }
            }
            context.Logger.LogLine($"Thumbnail created for {s3Event.Object.Key}");
            return $"Thumbnail created for {s3Event.Object.Key}";
        }
        catch (Exception e)
        {
            context.Logger.LogLine($"Error processing {s3Event.Object.Key}: {e.Message}");
            return $"Error processing {s3Event.Object.Key}: {e.Message}";
        }
    }

}