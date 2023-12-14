using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSserverlessApk
{
    public class Function
    {
        private readonly IAmazonRekognition _rekognitionClient;
        private readonly IAmazonDynamoDB _dynamoDbClient;

        public Function()
        {
            _rekognitionClient = new AmazonRekognitionClient();
            _dynamoDbClient = new AmazonDynamoDBClient();
        }

        public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
        {
            foreach (var record in s3Event.Records)
            {
                var s3ObjectKey = record.S3.Object.Key;
                var imageUrl = $"s3://{record.S3.Bucket.Name}/{s3ObjectKey}";

                // Use Rekognition service to detect labels
                var detectedLabels = await DetectLabelsAsync(imageUrl);

                // Filter labels with confidence > 90
                var highConfidenceLabels = detectedLabels.Labels
                    .Where(label => label.Confidence > 90)
                    .Select(label => new { Name = label.Name, Confidence = label.Confidence })
                    .ToList();

                // Construct DynamoDB item with image URL and high-confidence labels
                var item = new Document();
                item["ImageID"] = Guid.NewGuid().ToString();
                item["ImageURL"] = imageUrl;

                // Create a DynamoDBList to hold label information
                var labelList = highConfidenceLabels
                    .Select(label => new DynamoDBEntry[]
                    {
                        new Primitive(label.Name),
                        new Primitive(label.Confidence.ToString())
                    })
                    .SelectMany(x => x)
                    .ToList();

                // Assign the labelList to the Labels attribute in the Document
                item["Labels"] = new DynamoDBList(labelList);

                // Insert item into DynamoDB table
                await InsertItemToDynamoDBAsync(item);
            }
        }

        private async Task<DetectLabelsResponse> DetectLabelsAsync(string imageUrl)
        {
            var request = new DetectLabelsRequest
            {
                Image = new Image
                {
                    S3Object = new Amazon.Rekognition.Model.S3Object
                    {
                        Bucket = "images-detection", // Replace with your S3 bucket name
                        Name = imageUrl.Substring(imageUrl.IndexOf("images-detection") + "images-detection/".Length)
                    }
                },
                MinConfidence = 90F
            };

            var response = await _rekognitionClient.DetectLabelsAsync(request);
            return response;
        }

        private async Task InsertItemToDynamoDBAsync(Document item)
        {
            Table table = Table.LoadTable(_dynamoDbClient, "ImageLabelsTable");

            await table.PutItemAsync(item);
        }
    }
}
