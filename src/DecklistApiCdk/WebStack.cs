using System;
using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AWS.S3;
using Constructs;
using DecklistApiCdk;

namespace MtgDecklistsCdk
{
    public class WebStack : Stack
    {
        internal WebStack(ResourceStack resourceStack, Use1ResourceStack use1ResourceStack, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //Lambda Function containing the webapi
            var decklistApiImageFunction = new DockerImageFunction(this, "decklist-api-lambda-function", new DockerImageFunctionProps
            {
                FunctionName = "decklist-api",
                Description = "Executes docker image containing the asp.net code for the API",
                Code = DockerImageCode.FromEcr(resourceStack.EcrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = Program.DecklistApiAotImageTag
                }),
                Architecture = Architecture.ARM_64,
                Timeout = Duration.Seconds(10),
                MemorySize = 256,
                Tracing = Tracing.ACTIVE,
            });

            var lambdaFunctionUrl = decklistApiImageFunction.AddFunctionUrl(new FunctionUrlOptions {
                AuthType = FunctionUrlAuthType.NONE,
                InvokeMode = InvokeMode.BUFFERED
            });

            //Allow it's assigned role to pull from the ECR repo containing the image
            resourceStack.EcrRepo.GrantPull(decklistApiImageFunction.Role);
            
            resourceStack.ScryfallDdbTable.GrantReadData(decklistApiImageFunction.Role);
            resourceStack.ScryfallDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");
            
            resourceStack.DecklistApiUsersDdbTable.GrantReadWriteData(decklistApiImageFunction.Role);
            resourceStack.DecklistApiUsersDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");

            resourceStack.DecklistApiEventsDdbTable.GrantReadWriteData(decklistApiImageFunction.Role);
            resourceStack.DecklistApiEventsDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");

            resourceStack.DecklistApiDecksDdbTable.GrantReadWriteData(decklistApiImageFunction.Role);
            resourceStack.DecklistApiDecksDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");

            decklistApiImageFunction.Role.AddManagedPolicy(ManagedPolicy.FromManagedPolicyArn(this, "decklist-api-lambda-xray-write-policy", "arn:aws:iam::aws:policy/AWSXrayWriteOnlyAccess"));

            //A cloudfront function that will rewrite certain paths to request index.html.
            var reactRouterFunction = new Amazon.CDK.AWS.CloudFront.Function(this, "decklist-cloudfront-function-react-router", new Amazon.CDK.AWS.CloudFront.FunctionProps {
                Code = FunctionCode.FromFile(new FileCodeOptions {
                    FilePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "FunctionCode", "react-router-cloudfront-function.js")
                }),
                Runtime = FunctionRuntime.JS_2_0,
                FunctionName = "react-route-handler",
                AutoPublish = true
            });

            resourceStack.WebsiteS3Bucket.Policy = new BucketPolicy(this, "decklist-website-bucket-policy", new BucketPolicyProps {
                RemovalPolicy = RemovalPolicy.DESTROY,
                Bucket = resourceStack.WebsiteS3Bucket,
            });

            var cfBucketAccessControl = new S3OriginAccessControl(this, "decklist-website-cloudfront-oac", new S3OriginAccessControlProps {
                Signing = Signing.SIGV4_NO_OVERRIDE,
                OriginAccessControlName = "decklist-website-s3-oac",
                Description = "Allows access from Cloudfront to website bucket"
            });

            var s3Origin = S3BucketOrigin.WithOriginAccessControl(resourceStack.WebsiteS3Bucket, new S3BucketOriginWithOACProps {
                OriginAccessControl = cfBucketAccessControl,
                OriginId = "decklist-website-s3-bucket",
                OriginPath = Program.DecklistWebsiteVersion,
            });

            var cloudfrontDistribution = new Distribution(this, "decklist-cloudfront-distribution", new DistributionProps {
                DefaultRootObject = "index.html",
                Certificate = use1ResourceStack.TlsCertificateForCloudFront,
                DomainNames = new[]{ Program.DomainName, $"www.{Program.DomainName}" },
                PriceClass = PriceClass.PRICE_CLASS_100,
                DefaultBehavior = new BehaviorOptions {
                    Origin = s3Origin,
                    AllowedMethods = AllowedMethods.ALLOW_GET_HEAD,
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    CachePolicy = new CachePolicy(this, "decklist-website-cache-policy", new CachePolicyProps {
                        CachePolicyName = "react-spa-cache-policy",
                        Comment = "Policy optimized for a react native app that rarely changes",
                        EnableAcceptEncodingBrotli = true,
                        EnableAcceptEncodingGzip = true,
                        DefaultTtl = Duration.Seconds(60),
                        MaxTtl = Duration.Minutes(10),
                        MinTtl = Duration.Seconds(0)
                    }),
                    FunctionAssociations = new []{
                        new FunctionAssociation {
                            EventType = FunctionEventType.VIEWER_REQUEST,
                            Function = reactRouterFunction
                        }
                    }
                },
                AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>{
                    { "/api/*", new BehaviorOptions {
                        CachePolicy = CachePolicy.CACHING_DISABLED,
                        AllowedMethods = AllowedMethods.ALLOW_ALL,
                        Origin = new FunctionUrlOrigin(lambdaFunctionUrl),
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY,
                        OriginRequestPolicy = new OriginRequestPolicy(this, "decklist-api-cf-behavior", new OriginRequestPolicyProps {
                            CookieBehavior = OriginRequestCookieBehavior.AllowList("decklist-api-auth"),
                            QueryStringBehavior = OriginRequestQueryStringBehavior.AllowList("q")
                        }),
                    }}
                }
            });

            new ARecord(this, "decklist-lol-cloudfront-alias", new ARecordProps {
                Zone = resourceStack.decklist_lol_publicHostedZone,
                Target = RecordTarget.FromAlias(new CloudFrontTarget(cloudfrontDistribution))
            });

            new ARecord(this, "decklist-lol-cloudfront-alias-www", new ARecordProps {
                RecordName = "www",
                Zone = resourceStack.decklist_lol_publicHostedZone,
                Target = RecordTarget.FromAlias(new CloudFrontTarget(cloudfrontDistribution))
            });
        }
    }
}