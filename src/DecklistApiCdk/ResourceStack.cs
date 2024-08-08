using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace DecklistApiCdk
{
    public class ResourceStack : Stack
    {
        public Repository EcrRepo;
        public Bucket WebsiteS3Bucket;
        public OriginAccessIdentity WebsiteS3BucketOai;
        public TableV2 ScryfallDdbTable;
        public TableV2 DecklistApiUsersDdbTable;
        public TableV2 DecklistApiEventsDdbTable;
        public TableV2 DecklistApiDecksDdbTable;

        internal ResourceStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //DynamoDb Table
            ScryfallDdbTable = new TableV2(this, "ddb-table-scryfall-data", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "first_letter", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "card_name_sort", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "scryfall-card-data",
            });

            DecklistApiUsersDdbTable = new TableV2(this, "ddb-table-decklist-api-users", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "user_email_hash", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "decklist-api-users",
                TimeToLiveAttribute = "__expires_ttl"
            });

            DecklistApiEventsDdbTable = new TableV2(this, "ddb-table-decklist-api-events", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "user_email_hash", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "decklist-api-events",
                TimeToLiveAttribute = "__expires_ttl"
            });

            DecklistApiDecksDdbTable = new TableV2(this, "ddb-table-decklist-api-decks", new TablePropsV2 {
                PartitionKey = new Attribute { Name = "event_id", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TableClass = TableClass.STANDARD,
                TableName = "decklist-api-decks",
                TimeToLiveAttribute = "__expires_ttl"
            });

            EcrRepo = new Repository(this, "decklist-api-repo", new RepositoryProps {
                ImageScanOnPush = true,
                RepositoryName = "decklist-api-container-repo",
                ImageTagMutability = TagMutability.IMMUTABLE
            });

            WebsiteS3BucketOai = new OriginAccessIdentity(this, "decklist-api-cf-oai", new OriginAccessIdentityProps {
                Comment = "OAI for decklist-api s3 access"
            });

            WebsiteS3Bucket = new Bucket(this, "decklist-api-website-s3-bucket", new BucketProps {
                BucketName = "decklist-api-website",
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                Cors = new [] {
                    new CorsRule {
                        AllowedMethods = new[]{ HttpMethods.GET, HttpMethods.HEAD },
                        AllowedOrigins = new[]{ "*" },
                        AllowedHeaders = new[]{ "*" },
                        MaxAge = 300
                    }
                }
            });

            WebsiteS3Bucket.GrantRead(WebsiteS3BucketOai);
        }
    }
}
