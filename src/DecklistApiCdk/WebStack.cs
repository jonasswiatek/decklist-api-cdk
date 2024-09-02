using System;
using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AwsApigatewayv2Integrations;
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
                    TagOrDigest = Program.DecklistApiImageTag
                }),
                Timeout = Duration.Seconds(30),
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



            //Lambda Function containing the webapi
            var decklistApiAotImageFunction = new DockerImageFunction(this, "decklist-api-aot-lambda-function", new DockerImageFunctionProps
            {
                FunctionName = "decklist-api-aot",
                Description = "Executes docker image containing the asp.net code for the API",
                Code = DockerImageCode.FromEcr(resourceStack.EcrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = Program.DecklistApiAotImageTag
                }),
                Timeout = Duration.Seconds(5),
                MemorySize = 256,
                Tracing = Tracing.ACTIVE,
            });

            //Allow it's assigned role to pull from the ECR repo containing the image
            resourceStack.EcrRepo.GrantPull(decklistApiAotImageFunction.Role);
            
            resourceStack.ScryfallDdbTable.GrantReadData(decklistApiAotImageFunction.Role);
            resourceStack.ScryfallDdbTable.Grant(decklistApiAotImageFunction.Role, "dynamodb:PartiQLSelect");
            resourceStack.DecklistApiUsersDdbTable.GrantReadWriteData(decklistApiAotImageFunction.Role);
            resourceStack.DecklistApiEventsDdbTable.GrantReadWriteData(decklistApiAotImageFunction.Role);
            resourceStack.DecklistApiDecksDdbTable.GrantReadWriteData(decklistApiAotImageFunction.Role);

            decklistApiAotImageFunction.Role.AddManagedPolicy(ManagedPolicy.FromManagedPolicyArn(this, "decklist-api-aot-lambda-xray-write-policy", "arn:aws:iam::aws:policy/AWSXrayWriteOnlyAccess"));

            //Wrap as an HttpLambdaIntegration object that API Gateway understands.
            var deckcheckApiAotLambda = new HttpLambdaIntegration("decklist-api-gateway-lambda-integration", decklistApiAotImageFunction);



            //The API Gateway itself, configured as Http API
            var httpApi = new HttpApi(this, "decklist-api-gateway", new HttpApiProps { });

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
                Path = "/api/aot/cards/search",
                Methods = new [] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.GET },
                Integration = deckcheckApiAotLambda
            });

            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/me",
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

            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/events/{proxy+}",
                Methods = new [] { 
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.GET,
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.DELETE,
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.POST
                },
                Integration = deckcheckApiLambda
            });

            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/api/decks/{proxy+}",
                Methods = new [] { 
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.GET,
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.DELETE,
                    Amazon.CDK.AWS.Apigatewayv2.HttpMethod.POST
                },
                Integration = deckcheckApiLambda
            });

            //A cloudfront function that will rewrite certain paths to request index.html.
            var reactRouterFunction = new Amazon.CDK.AWS.CloudFront.Function(this, "decklist-cloudfront-function-react-router", new Amazon.CDK.AWS.CloudFront.FunctionProps {
                Code = FunctionCode.FromFile(new FileCodeOptions {
                    FilePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "FunctionCode", "react-router-cloudfront-function.js")
                }),
                Runtime = FunctionRuntime.JS_2_0,
                FunctionName = "react-route-handler",
                AutoPublish = true
            });

            var cloudfrontDistribution = new Distribution(this, "decklist-cloudfront-distribution", new DistributionProps {
                DefaultRootObject = "index.html",
                Certificate = use1ResourceStack.TlsCertificateForCloudFront,
                DomainNames = new[]{ Program.DomainName },
                DefaultBehavior = new BehaviorOptions {
                    Origin = new S3Origin(resourceStack.WebsiteS3Bucket, new S3OriginProps {
                        OriginId = "decklist-api-website-s3-bucket",
                        OriginPath = Program.DecklistWebsiteVersion,
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
                        Origin = new HttpOrigin($"{httpApi.HttpApiId}.execute-api.{Region}.amazonaws.com", new HttpOriginProps {
                            OriginId = "decklist-api-gateway",
                        }),
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY,
                        OriginRequestPolicy = new OriginRequestPolicy(this, "decklist-api-cf-behavior", new OriginRequestPolicyProps {
                            CookieBehavior = OriginRequestCookieBehavior.AllowList("decklist-api-auth"),
                            QueryStringBehavior = OriginRequestQueryStringBehavior.AllowList("q")
                        })
                    }}
                }
            });

            new ARecord(this, "decklist-lol-cloudfront-alias", new ARecordProps {
                Zone = resourceStack.decklist_lol_publicHostedZone,
                Target = RecordTarget.FromAlias(new CloudFrontTarget(cloudfrontDistribution))
            });
        }
    }
}