using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.AppSync;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;
using MtgDecklistsCdk;

namespace DecklistApiCdk;

    public class ScryfallStack : Stack
    {
        internal ScryfallStack(ResourceStack resourceStack, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var queue = new Queue(this, "scryfall-import-queue", new QueueProps {
                QueueName = "scryfall-import"
            });

            //Lambda Function containing the webapi
            var scryfallReaderImageFunction = new DockerImageFunction(this, "scryfall-reader-lambda-function", new DockerImageFunctionProps
            {
                FunctionName = "scryfall-reader",
                Description = "Loads all magic cards from scryfall bulk api and shoves json on an SQS queue for ingestion into DynamoDB",
                Code = DockerImageCode.FromEcr(resourceStack.EcrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = Program.ScryfalllReaderImageTag,
                    Cmd = new [] {"DecklistApi.ScryfallReader::DecklistApi.ScryfallReader.ScryfallBulkImport::ReadToSqs"},
                }),
                Environment = new Dictionary<string, string> {
                    { "sqs_queue_url", queue.QueueUrl }
                },
                Timeout = Duration.Seconds(120),
                MemorySize = 512
            });

            queue.GrantSendMessages(scryfallReaderImageFunction.Role);

            var dynamodbWriterImageFunction = new DockerImageFunction(this, "scryfall-ddb-writer-lambda-function", new DockerImageFunctionProps
            {
                FunctionName = "scryfall-ddb-writer",
                Description = "Writes magic cards from SQS into DynamoDb",
                Code = DockerImageCode.FromEcr(resourceStack.EcrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = Program.ScryfalllReaderImageTag,
                    Cmd = new [] {"DecklistApi.ScryfallReader::DecklistApi.ScryfallReader.ScryfallBulkImport::WriteToDynamoDb"},
                }),
                Environment = new Dictionary<string, string> {
                    { "sqs_queue_url", queue.QueueUrl }
                },
                Timeout = Duration.Seconds(10),
                MemorySize = 128
            });

            queue.GrantSendMessages(dynamodbWriterImageFunction.Role);
            dynamodbWriterImageFunction.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps
            {
                BatchSize = 10,
                Enabled = true,
                MaxBatchingWindow = Duration.Seconds(10),
                MaxConcurrency = 10,
            }));

            resourceStack.ScryfallDdbTable.GrantWriteData(dynamodbWriterImageFunction.Role);
        }
    }

