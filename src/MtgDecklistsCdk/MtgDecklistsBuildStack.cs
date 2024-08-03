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
    public class MtgDecklistsBuildStack : Stack
    {
        public Repository EcrRepo;

        internal MtgDecklistsBuildStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            EcrRepo = new Repository(this, "decklist-api-repo", new RepositoryProps {
                ImageScanOnPush = true,
                RepositoryName = "decklist-api-container-repo",
                ImageTagMutability = TagMutability.IMMUTABLE
            });

            var decklistApiBuild = new Project(this, "decklist-api", new ProjectProps {
                ProjectName = "decklist-api",
                Source = Source.GitHub(new GitHubSourceProps {
                    Owner = "jonasswiatek",
                    Repo = "decklist-api",
                }),
                Environment = new BuildEnvironment {
                    ComputeType = ComputeType.SMALL
                },
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>()
                {
                    {"ecr_repo_name", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT, Value = EcrRepo.RepositoryName} },
                    {"ecr_repo_uri", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT, Value = EcrRepo.RepositoryUri} },
                },
                BuildSpec = BuildSpec.FromObject(new Dictionary<string, object> {
                    { "version", "0.2" },
                    { "phases", new Dictionary<string, Dictionary<string, string[]>> {
                        {
                            "pre_build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    {
                                        "PROJECT_NAME=DeckistApi.Web",
                                        "aws ecr get-login-password | docker login --username AWS --password-stdin $ecr_repo_uri",
                                        "IMAGE_TAG=${PROJECT_NAME}-$CODEBUILD_BUILD_NUMBER"
                                    } 
                                }
                            }
                        },
                        {
                            "build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    { 
                                        "docker build -t $ecr_repo_uri:$IMAGE_TAG ./src",
                                    } 
                                }
                            }
                        },
                        {
                            "post_build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    { 
                                        "docker push $ecr_repo_uri:$IMAGE_TAG",
                                    } 
                                }
                            }
                        }

                    } }
                })
            });

            EcrRepo.GrantPush(decklistApiBuild.Role);
        }
    }
}
