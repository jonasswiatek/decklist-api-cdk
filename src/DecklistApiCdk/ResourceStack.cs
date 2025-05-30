using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SES;
using Amazon.CDK.AWS.SSM;
using Constructs;
using MtgDecklistsCdk;

namespace DecklistApiCdk
{
    public class ResourceStack : Stack
    {
        public Repository EcrRepo;

        public Bucket WebsiteS3Bucket;

        public TableV2 ScryfallDdbTable;
        public TableV2 DecklistApiUsersDdbTable;
        public TableV2 DecklistApiEventsDdbTable;
        public TableV2 DecklistApiDecksDdbTable;

        public IHostedZone decklist_lol_publicHostedZone;
        public EmailIdentity EmailIdentity;

        public StringParameter googleSigninClientIdParameter;
        public StringParameter sendgridApiKeyParameter;
        public StringParameter emailHashPepperParameter;
        public StringParameter jwtSigningKeyParameter;
        public StringParameter jwtEncryptionKeyParameter;

        internal ResourceStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //DynamoDb Table
            ScryfallDdbTable = new TableV2(this, "ddb-table-scryfall-data", new TablePropsV2 {
                TableName = "scryfall-card-data",
                PartitionKey = new Attribute { Name = "first_letter", Type = AttributeType.STRING },
                SortKey = new Attribute { Name = "card_name_sort", Type = AttributeType.STRING },

                /*
                Billing = Billing.Provisioned(new ThroughputProps {
                    ReadCapacity = Capacity.Fixed(12),
                    WriteCapacity = Capacity.Autoscaled(new AutoscaledCapacityOptions { MaxCapacity = 10, SeedCapacity = 5, MinCapacity = 5 })
                }),
                */

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
                    ReadCapacity = Capacity.Fixed(25),
                    WriteCapacity = Capacity.Autoscaled(new AutoscaledCapacityOptions { MaxCapacity = 25, SeedCapacity = 25, MinCapacity = 25 })
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

            WebsiteS3Bucket = new Bucket(this, "decklist-api-website-s3-bucket", new BucketProps {
                BucketName = "decklist-api-website",
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                EnforceSSL = true,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            sendgridApiKeyParameter = new StringParameter(this, "DecklistApiSendKeyApiKeyParameter", new StringParameterProps
            {
                ParameterName = "/decklist-api/config/sendgrid-email-api-key",
                StringValue = "insert-value",
                Description = "Sendgrid key",
                Tier = ParameterTier.STANDARD,
            });

            googleSigninClientIdParameter = new StringParameter(this, "DecklistApiGoogleClientIdParameter", new StringParameterProps
            {
                ParameterName = "/decklist-api/config/google-client-id",
                StringValue = "insert-value",
                Description = "Google CliendId key",
                Tier = ParameterTier.STANDARD,
            });

            emailHashPepperParameter = new StringParameter(this, "DecklistApiEmailHashPepperParameter", new StringParameterProps
            {
                ParameterName = "/decklist-api/config/email-hash-pepper",
                StringValue = "insert-value",
                Description = "Pepper value for email hashing",
                Tier = ParameterTier.STANDARD,
            });

            jwtSigningKeyParameter = new StringParameter(this, "DecklistApiJwtSigningKeyParameter", new StringParameterProps
            {
                ParameterName = "/decklist-api/config/jwt-signing-key",
                StringValue = "insert-value",
                Description = "JWT Signing Key (32 bytes)",
                Tier = ParameterTier.STANDARD,
            });

            jwtEncryptionKeyParameter = new StringParameter(this, "DecklistApiJwtEncryptionKeyParameter", new StringParameterProps
            {
                ParameterName = "/decklist-api/config/jwt-encryption-key",
                StringValue = "insert-value",
                Description = "JWT Encryption Key (32 bytes)",
                Tier = ParameterTier.STANDARD,
            });

            decklist_lol_publicHostedZone = HostedZone.FromHostedZoneAttributes(this, "decklist-public-hosted-zone", new HostedZoneAttributes {
                HostedZoneId = "Z0023525188L7JL55G3HG",
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


            //AWS SES
            EmailIdentity = new EmailIdentity(this, "Identity", new EmailIdentityProps {
                Identity = Identity.Domain(Program.DomainName),
                MailFromDomain = "mail.decklist.lol",
            });
            

            new CnameRecord(this, "decklist-lol-ses-dkim-1", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = Fn.Select(0, Fn.Split($".{Program.DomainName}", EmailIdentity.DkimDnsTokenName1)),
                DomainName = EmailIdentity.DkimDnsTokenValue1,
                Ttl = Duration.Hours(3)
            });

            new CnameRecord(this, "decklist-lol-ses-dkim-2", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = Fn.Select(0, Fn.Split($".{Program.DomainName}", EmailIdentity.DkimDnsTokenName2)),
                DomainName = EmailIdentity.DkimDnsTokenValue2,
                Ttl = Duration.Hours(3)
            });

            new CnameRecord(this, "decklist-lol-ses-dkim-3", new CnameRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = Fn.Select(0, Fn.Split($".{Program.DomainName}", EmailIdentity.DkimDnsTokenName3)),
                DomainName = EmailIdentity.DkimDnsTokenValue3,
                Ttl = Duration.Hours(3)
            });

            new MxRecord(this, "decklist-lol-ses-mx", new MxRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "mail",
                Values = new [] { new MxRecordValue { Priority = 10, HostName = "feedback-smtp.eu-central-1.amazonses.com" } },
                Ttl = Duration.Hours(3)
            });
            
            new TxtRecord(this, "decklist-lol-ses-txt", new TxtRecordProps {
                Zone = decklist_lol_publicHostedZone,
                RecordName = "mail",
                Values = new [] { $"v=spf1 include:amazonses.com ~all" },
                Ttl = Duration.Hours(3)
            });
        }
    }
}
