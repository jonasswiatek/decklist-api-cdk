using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;
using MtgDecklistsCdk;

namespace DecklistApiCdk
{
    /// <summary>
    /// The reader function can be invoked with
    /// aws lambda invoke --function-name scryfall-reader --payload '{ "LookbackDays": 20000 }' --cli-binary-format raw-in-base64-out response.json --profile decklist-api-prod
    /// </summary>
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
                MemorySize = 256
            });

            queue.GrantSendMessages(scryfallReaderImageFunction.Role);

            //Configure schedule for the reader function with event bridge
            var rule = new Rule(this, "scryfall-reader-schedule-rule", new RuleProps {
                RuleName = "scryfall-scrape-schedule",
                Enabled = true,
                Schedule = Schedule.Cron(new CronOptions {
                    WeekDay = "Friday",
                    Hour = "12",
                    Minute = "00",
                })
            });

            rule.AddTarget(new LambdaFunction(scryfallReaderImageFunction, new LambdaFunctionProps {
                Event = RuleTargetInput.FromObject(
                    new Dictionary<string, int> {
                        { "LookbackDays", 30 }
                    }
                )
            }));

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
                MemorySize = 256
            });

            queue.GrantSendMessages(dynamodbWriterImageFunction.Role);
            dynamodbWriterImageFunction.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps
            {
                BatchSize = 10,
                Enabled = true,
                MaxBatchingWindow = Duration.Seconds(10),
                MaxConcurrency = 2,
            }));

            resourceStack.ScryfallDdbTable.GrantWriteData(dynamodbWriterImageFunction.Role);
        }
    }
}