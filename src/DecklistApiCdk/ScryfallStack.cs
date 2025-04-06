using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
using Constructs;
using MtgDecklistsCdk;

namespace DecklistApiCdk
{
    /// <summary>
    /// The reader function can be invoked with
    /// aws lambda invoke --function-name scryfall-import --payload '{ "LookbackDays": 30 }' --cli-binary-format raw-in-base64-out response.json --profile decklist
    /// </summary>
    public class ScryfallStack : Stack
    {
        internal ScryfallStack(ResourceStack resourceStack, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //Lambda Function containing the webapi
            var scryfallReaderImageFunction = new DockerImageFunction(this, "scryfall-reader-lambda-function", new DockerImageFunctionProps
            {
                FunctionName = "scryfall-import",
                Description = "Loads all magic cards from scryfall bulk api and shoves them into DynamoDB",
                Code = DockerImageCode.FromEcr(resourceStack.EcrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = Program.ScryfalllReaderImageTag,
                }),
                Timeout = Duration.Seconds(300),
                MemorySize = 256
            });

            resourceStack.ScryfallDdbTable.GrantWriteData(scryfallReaderImageFunction.Role);

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
        }
    }
}