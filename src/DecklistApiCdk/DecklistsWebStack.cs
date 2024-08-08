using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using DecklistApiCdk;

namespace MtgDecklistsCdk
{
    public class DecklistsWebStack : Stack
    {
        internal DecklistsWebStack(ResourceStack resourceStack, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var decklistApiImageTag = "DecklistApi.Web-1";
            var decklistWebsiteVersion = "v1.0.1";

            //Lambda Function containing the webapi
            var decklistApiImageFunction = new DockerImageFunction(this, "decklist-api-lambda-function", new DockerImageFunctionProps
            {
                FunctionName = "decklist-api",
                Description = "Executes docker image containing the asp.net code for the API",
                Code = DockerImageCode.FromEcr(resourceStack.EcrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = decklistApiImageTag
                }),
                Timeout = Duration.Seconds(20),
                MemorySize = 512,
                Tracing = Tracing.ACTIVE,
            });

            //Allow it's assigned role to pull from the ECR repo containing the image
            resourceStack.EcrRepo.GrantPull(decklistApiImageFunction.Role);
            
            resourceStack.ScryfallDdbTable.GrantReadData(decklistApiImageFunction.Role);
            resourceStack.ScryfallDdbTable.Grant(decklistApiImageFunction.Role, "dynamodb:PartiQLSelect");
            resourceStack.DecklistApiUsersDdbTable.GrantReadWriteData(decklistApiImageFunction.Role);
            resourceStack.DecklistApiEventsDdbTable.GrantReadWriteData(decklistApiImageFunction.Role);
            resourceStack.DecklistApiDecksDdbTable.GrantReadWriteData(decklistApiImageFunction.Role);

            decklistApiImageFunction.Role.AddManagedPolicy(ManagedPolicy.FromManagedPolicyArn(this, "decklist-api-lambda-xray-write-policy", "arn:aws:iam::aws:policy/AWSXrayWriteOnlyAccess"));

            //Wrap as an HttpLambdaIntegration object that API Gateway understands.
            var deckcheckApiLambda = new HttpLambdaIntegration("decklist-api-gateway-lambda-integration", decklistApiImageFunction);

            //Certificate and domain name for API Gateway
            var domainName = "decklist.lol";
            var certificateUse1 = Certificate.FromCertificateArn(this, "decklist-api-cert-use1", "arn:aws:acm:us-east-1:017820661759:certificate/5e784e58-7748-415e-a431-f36a3a57b84e");
            var certificateEuc1 = Certificate.FromCertificateArn(this, "decklist-api-cert-euc1", "arn:aws:acm:eu-central-1:017820661759:certificate/25d4b571-d70b-4664-8c77-d7b02864638d");
            
            var dn = new DomainName(this, "decklist-api-domain-name", new DomainNameProps {
                DomainName = domainName,
                Certificate = certificateEuc1
            });

            //The API Gateway itself, configured as Http API
            var httpApi = new HttpApi(this, "decklist-api-gateway", new HttpApiProps {
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

            new Distribution(this, "decklist-cloudfront-distribution", new DistributionProps {
                DefaultRootObject = "index.html",
                Certificate = certificateUse1,
                DomainNames = new[]{ domainName },
                DefaultBehavior = new BehaviorOptions {
                    Origin = new S3Origin(resourceStack.WebsiteS3Bucket, new S3OriginProps {
                        OriginId = "decklist-api-website-s3-bucket",
                        OriginPath = decklistWebsiteVersion,
                        OriginAccessIdentity = resourceStack.WebsiteS3BucketOai,
                    }),
                    AllowedMethods = AllowedMethods.ALLOW_GET_HEAD,
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    CachePolicy = new CachePolicy(this, "decklist-website-cache-policy", new CachePolicyProps {
                        CachePolicyName = "react-spa-cache-policy",
                        Comment = "Policy optimized for a react native app that rarely changes",
                        EnableAcceptEncodingBrotli = true,
                        EnableAcceptEncodingGzip = true,
                        DefaultTtl = Duration.Seconds(10),
                        MaxTtl = Duration.Seconds(10),
                        MinTtl = Duration.Seconds(0)
                    }) 
                },
                AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>{
                    { "/api/*", new BehaviorOptions {
                        CachePolicy = CachePolicy.CACHING_DISABLED,
                        AllowedMethods = AllowedMethods.ALLOW_ALL,
                        Origin = new HttpOrigin($"{httpApi.HttpApiId}.execute-api.{Region}.amazonaws.com", new HttpOriginProps {
                            OriginId = "decklist-api-gateway",
                        }),
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY,
                        OriginRequestPolicy = new OriginRequestPolicy(this, "decklist-api-cf-behavior", new OriginRequestPolicyProps {
                            CookieBehavior = OriginRequestCookieBehavior.AllowList("decklist-api-auth"),
                        })
                    }}
                }
            });
        }
    }
}