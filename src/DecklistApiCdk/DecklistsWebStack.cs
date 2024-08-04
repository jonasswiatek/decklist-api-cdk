using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;

namespace MtgDecklistsCdk
{
    public class DecklistsWebStack : Stack
    {
        internal DecklistsWebStack(Repository ecrRepo, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //Lambda Function containing the webapi
            var decklistApiImageFunction = new DockerImageFunction(this, "DecklistApiLambdaImageFunction", new DockerImageFunctionProps {
                Code = DockerImageCode.FromEcr(ecrRepo, new EcrImageCodeProps
                {
                    TagOrDigest = "DeckistApi.Web-2"
                } )
            });

            //Allow it's assigned role to pull from the ECR repo containing the image
            ecrRepo.GrantPull(decklistApiImageFunction.Role);

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
