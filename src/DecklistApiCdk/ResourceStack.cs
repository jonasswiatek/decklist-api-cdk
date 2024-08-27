using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.S3;
using Constructs;
using MtgDecklistsCdk;

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

        public PublicHostedZone decklist_lol_publicHostedZone;

        internal ResourceStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //DynamoDb Table
            ScryfallDdbTable = new TableV2(this, "ddb-table-scryfall-data", new TablePropsV2 {
                TableName = "scryfall-card-data",
                PartitionKey = new Attribute { Name = "first_letter", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "card_name_sort", Type = AttributeType.STRING },
                
                Billing = Billing.Provisioned(new ThroughputProps {
                    ReadCapacity = Capacity.Fixed(12),
                    WriteCapacity = Capacity.Autoscaled(new AutoscaledCapacityOptions { MaxCapacity = 5, SeedCapacity = 5 })
                }),

                TableClass = TableClass.STANDARD,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            DecklistApiUsersDdbTable = new TableV2(this, "ddb-table-decklist-api-users", new TablePropsV2 {
                TableName = "decklist-api-users",
                PartitionKey = new Attribute { Name = "user_email_hash", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TimeToLiveAttribute = "__expires_ttl",

                TableClass = TableClass.STANDARD,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            DecklistApiEventsDdbTable = new TableV2(this, "ddb-table-decklist-api-events", new TablePropsV2 {
                TableName = "decklist-api-events",
                PartitionKey = new Attribute { Name = "event_id", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TimeToLiveAttribute = "__expires_ttl",

                TableClass = TableClass.STANDARD,
                RemovalPolicy = RemovalPolicy.RETAIN,

                GlobalSecondaryIndexes = new [] {
                    new GlobalSecondaryIndexPropsV2() {
                        IndexName = "user-events-index",
                        PartitionKey = new Attribute { Name = "user_email_hash", Type = AttributeType.STRING },
                        SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                        ProjectionType = ProjectionType.ALL
                    }
                }
            });

            DecklistApiDecksDdbTable = new TableV2(this, "ddb-table-decklist-api-decks", new TablePropsV2 {
                TableName = "decklist-api-decks",
                PartitionKey = new Attribute { Name = "event_id", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "item", Type = AttributeType.STRING },
                TimeToLiveAttribute = "__expires_ttl",

                Billing = Billing.Provisioned(new ThroughputProps {
                    ReadCapacity = Capacity.Fixed(13),
                    WriteCapacity = Capacity.Autoscaled(new AutoscaledCapacityOptions { MaxCapacity = 20, SeedCapacity = 5 })
                }),

                TableClass = TableClass.STANDARD,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            EcrRepo = new Repository(this, "decklist-api-ecr-repo", new RepositoryProps {
                ImageScanOnPush = true,
                RepositoryName = "decklist-api-container-repo",
                ImageTagMutability = TagMutability.IMMUTABLE,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            WebsiteS3BucketOai = new OriginAccessIdentity(this, "decklist-website-cloudfront-oai", new OriginAccessIdentityProps {
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
                },
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            WebsiteS3Bucket.GrantRead(WebsiteS3BucketOai);

            decklist_lol_publicHostedZone = new PublicHostedZone(this, "decklist-hostedzone", new PublicHostedZoneProps {
                ZoneName = Program.DomainName
            });

            new CnameRecord(this, "decklist-lol-sg-cname-em7133", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "em7133",
                DomainName = "u46110892.wl143.sendgrid.net",
                Ttl = Duration.Hours(3)
            });

            new CnameRecord(this, "decklist-lol-sg-cname-s1_domainkey", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "s1._domainkey",
                DomainName = "s1.domainkey.u46110892.wl143.sendgrid.net",
                Ttl = Duration.Hours(3)
            });

            new CnameRecord(this, "decklist-lol-sg-cname-s2_domainkey", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "s2._domainkey",
                DomainName = "s2.domainkey.u46110892.wl143.sendgrid.net",
                Ttl = Duration.Hours(3)
            });

            new TxtRecord(this, "decklist-lol-sg-txt-_dmarc", new TxtRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "_dmarc",
                Values = new [] { "v=DMARC1; p=none; rua=mailto:dmarc_agg@vali.email;" },
                Ttl = Duration.Hours(3)
            });

            new CnameRecord(this, "decklist-lol-sg-cname-url7648", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "url7648",
                DomainName = "sendgrid.net",
                Ttl = Duration.Hours(3)
            });

            new CnameRecord(this, "decklist-lol-sg-cname-46110892", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "46110892",
                DomainName = "sendgrid.net",
                Ttl = Duration.Hours(3)
            });
        }
    }
}
