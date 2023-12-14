using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using GrapeCity.Documents.Imaging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImagesToThumbnail
{
    public class Function
    {
        private const string DestinationBucketName = "imaging-destination"; // Replace with your destination bucket name
        private const string StateMachineName = "ImageProcessingStateMachine"; // Replace with your state machine name
        private readonly IAmazonS3 _s3Client = new AmazonS3Client();
        private readonly IAmazonStepFunctions _stepFunctionsClient = new AmazonStepFunctionsClient();

        public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            try
            {
                foreach (var record in evnt.Records)
                {
                    var bucketName = record.S3.Bucket.Name;
                    var key = record.S3.Object.Key;

                    // Check if the event is for the original-images bucket
                    if (bucketName != "original-images")
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

                // Automate the state machine creation
                await CreateStateMachine();
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Error: {ex.Message}");
                throw;
            }
        }

        private async Task CreateStateMachine()
        {
            // Define your state machine definition (Amazon States Language or ASL)
            var stateMachineDefinition = @"
                {
                    ""Comment"": ""Image Processing State Machine"",
                    ""StartAt"": ""GenerateThumbnail"",
                    ""States"": {
                        ""GenerateThumbnail"": {
                            ""Type"": ""Task"",
                            ""Resource"": ""arn:aws:lambda:ca-central-1:024255299146:function:Images2Thumbnail"",
                            ""End"": true
                        }
                        // Add more states as needed
                    }
                }
            ";

            // Create state machine request
            var createStateMachineRequest = new CreateStateMachineRequest
            {
                Name = StateMachineName,
                Definition = stateMachineDefinition,
                RoleArn = "arn:aws:iam::024255299146:role/thumbnail" // Replace with your state machine execution role ARN
            };

            // Create and deploy state machine
            var createStateMachineResponse = await _stepFunctionsClient.CreateStateMachineAsync(createStateMachineRequest);
            var stateMachineArn = createStateMachineResponse.StateMachineArn;

            // Start state machine execution (optional)
            await StartStateMachineExecution(stateMachineArn);
        }

        private async Task StartStateMachineExecution(string stateMachineArn)
        {
            // Start state machine execution request
            var startExecutionRequest = new StartExecutionRequest
            {
                StateMachineArn = stateMachineArn,
                Input = "{}" // Optionally, provide input data in JSON format
            };

            // Start state machine execution
            var startExecutionResponse = await _stepFunctionsClient.StartExecutionAsync(startExecutionRequest);
            var executionArn = startExecutionResponse.ExecutionArn;

            // Optionally, handle execution response or monitor execution status
        }
    }
}
