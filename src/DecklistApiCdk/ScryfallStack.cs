using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
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
                    TagOrDigest = Program.ScryfalllReaderImageTag
                }),
                Environment = new Dictionary<string, string> {
                    { "sqs_queue_url", queue.QueueUrl }
                },
                Timeout = Duration.Seconds(120),
                MemorySize = 512,
            });

            queue.GrantSendMessages(scryfallReaderImageFunction.Role);
        }
    }

