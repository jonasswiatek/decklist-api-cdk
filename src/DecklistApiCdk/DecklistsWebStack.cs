using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;

namespace MtgDecklistsCdk
{
    public class DecklistsWebStack : Stack
    {
        internal DecklistsWebStack(Repository ecrRepo, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //DynamoDb Table
            var scryfallDdbTable = new TableV2(this, "ddb-table-scryfall-data", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "first_letter", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "card_name_sort", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "scryfall-card-data",
            });

            var decklistApiUsersDdbTable = new TableV2(this, "ddb-table-decklist-api-users", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "user_email_hash", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "decklist-api-users",
                TimeToLiveAttribute = "__expires_ttl"
            });

            var decklistApiEventsDdbTable = new TableV2(this, "ddb-table-decklist-api-events", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "user_email_hash", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "decklist-api-events",
                TimeToLiveAttribute = "__expires_ttl"
            });

            var decklistApiDecksDdbTable = new TableV2(this, "ddb-table-decklist-api-decks", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "event_id", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "decklist-api-decks",
                TimeToLiveAttribute = "__expires_ttl"
            });

            //Lambda Function containing the webapi
            var decklistApiImageFunction = new DockerImageFunction(this, "DecklistApiLambdaImageFunction", new DockerImageFunctionProps {
                Code = DockerImageCode.FromEcr(ecrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = "DecklistApi.Web-15"
                }),
                Timeout = Duration.Seconds(20),
                MemorySize = 512,
                Tracing = Tracing.ACTIVE
            });

            //Allow it's assigned role to pull from the ECR repo containing the image
            ecrRepo.GrantPull(decklistApiImageFunction.Role);
            
            scryfallDdbTable.GrantReadData(decklistApiImageFunction.Role);
            scryfallDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");
            decklistApiUsersDdbTable.GrantReadData(decklistApiImageFunction.Role);
            decklistApiUsersDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");
            decklistApiEventsDdbTable.GrantReadData(decklistApiImageFunction.Role);
            decklistApiEventsDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");
            decklistApiDecksDdbTable.GrantReadData(decklistApiImageFunction.Role);
            decklistApiDecksDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");

            decklistApiImageFunction.Role.AddManagedPolicy(ManagedPolicy.FromManagedPolicyArn(this, "xray-write-policy", "arn:aws:iam::aws:policy/AWSXrayWriteOnlyAccess"));

            //Wrap as an HttpLambdaIntegration object that API Gateway understands.
            var deckcheckApiLambda = new HttpLambdaIntegration("DecklistApiIntegration", decklistApiImageFunction);

            //Certificate and domain name for API Gateway
            var certArn = "arn:aws:acm:eu-central-1:017820661759:certificate/25d4b571-d70b-4664-8c77-d7b02864638d";
            var domainName = "decklist.lol";

            var dn = new DomainName(this, "decklist-api-domain-name", new DomainNameProps {
                DomainName = domainName,
                Certificate = Certificate.FromCertificateArn(this, "decklist-api-cert", certArn)
            });

            //The API Gateway itself, configured as Http API
            var httpApi = new HttpApi(this, "decklist-api", new HttpApiProps {
                DefaultDomainMapping = new DomainMappingOptions {
                    DomainName = dn
                }
            });

            //And the associated routes which are all configured explicitly.
            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/login/start",
                Methods = new [] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.POST },
                Integration = deckcheckApiLambda
            });

            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/login/continue",
                Methods = new [] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.POST },
                Integration = deckcheckApiLambda
            });

            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/logout",
                Methods = new [] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.POST },
                Integration = deckcheckApiLambda
            });

            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/cards/search",
                Methods = new [] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.GET },
                Integration = deckcheckApiLambda
            });

            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/events",
                Methods = new [] { 
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.GET,
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.DELETE,
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.POST
                },
                Integration = deckcheckApiLambda
            });

        }
    }
}