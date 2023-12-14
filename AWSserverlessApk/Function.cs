using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;


using Amazon.Rekognition;
using Amazon.Rekognition.Model;

using Amazon.S3;
using Amazon.S3.Model;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSserverlessApk;

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
        var s3ObjectKey = s3Event.Records[0].S3.Object.Key;
        var imageUrl = $"s3://{s3Event.Records[0].S3.Bucket.Name}/{s3ObjectKey}";

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


    private async Task<DetectLabelsResponse> DetectLabelsAsync(string imageUrl)
    {
        var request = new DetectLabelsRequest
        {
            Image = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = "images-detection",
                    Name = "Editor_2_20220524215249.jpg"
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