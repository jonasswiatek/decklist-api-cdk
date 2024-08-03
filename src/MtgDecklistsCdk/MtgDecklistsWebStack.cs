using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;

namespace MtgDecklistsCdk
{
    public class MtgDecklistsWebStack : Stack
    {
        internal MtgDecklistsWebStack(Repository ecrRepo, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            Function decklistApiImageFunction = new DockerImageFunction(this, "DecklistLambdaImageFunction", new DockerImageFunctionProps {
                Code = DockerImageCode.FromEcr(ecrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = "DeckistApi.Web-1"
                } )
            });

            ecrRepo.GrantPull(decklistApiImageFunction.Role);

            var deckcheckApiLambda = new HttpLambdaIntegration("DecklistApiIntegration", decklistApiImageFunction);

            var httpApi = new HttpApi(this, "decklist-api");
            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/",
                Methods = new [] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.GET },
                Integration = deckcheckApiLambda
            });
            
            httpApi.AddRoutes(new AddRoutesOptions {
                Path = "/weatherforecast",
                Methods = new [] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.GET },
                Integration = deckcheckApiLambda
            });
        }
    }
}
