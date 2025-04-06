using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Constructs;
using DecklistApiCdk;

namespace MtgDecklistsCdk
{
    public class BuildStack : Stack
    {
        internal BuildStack(ResourceStack resourceStack, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var decklistApiAotBuild = new Project(this, "decklist-api-aot-build-project", new ProjectProps {
                ProjectName = "decklist-api-aot",
                Source = Source.GitHub(new GitHubSourceProps {
                    Owner = "jonasswiatek",
                    Repo = "decklist-api",
                }),
                Environment = new BuildEnvironment {
                    ComputeType = ComputeType.LARGE,
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_ARM_3
                },
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>()
                {
                    {"ecr_repo_name", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT, Value = resourceStack.EcrRepo.RepositoryName} },
                    {"ecr_repo_uri", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT, Value = resourceStack.EcrRepo.RepositoryUri} },
                },
                BuildSpec = BuildSpec.FromObject(new Dictionary<string, object> {
                    { "version", "0.2" },
                    { "phases", new Dictionary<string, Dictionary<string, string[]>> {
                        {
                            "pre_build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    {
                                        "PROJECT_NAME=DecklistApi.Web.Aot",
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
                                        "docker build -t $ecr_repo_uri:$IMAGE_TAG -f ./src/$PROJECT_NAME/Dockerfile ./src/",
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

            resourceStack.EcrRepo.GrantPush(decklistApiAotBuild.Role);

            var websiteBuildProject = new Project(this, "decklist-website-build-project", new ProjectProps {
                ProjectName = "decklist-website",
                Source = Source.GitHub(new GitHubSourceProps {
                    Owner = "jonasswiatek",
                    Repo = "decklist-website",
                }),
                Environment = new BuildEnvironment {
                    BuildImage = LinuxLambdaBuildImage.AMAZON_LINUX_2023_NODE_20
                },
                BuildSpec = BuildSpec.FromObject(new Dictionary<string, object> {
                    { "version", "0.2" },
                    { "phases", new Dictionary<string, Dictionary<string, string[]>> {
                        {
                            "pre_build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    {
                                        "cd src",
                                        "npm install",
                                    } 
                                }
                            }
                        },
                        {
                            "build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    { 
                                        "npm run build",
                                    } 
                                }
                            }
                        },
                        {
                            "post_build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    { 
                                        $"aws s3 cp ./dist s3://{resourceStack.WebsiteS3Bucket.BucketName}/v1.0.${{CODEBUILD_BUILD_NUMBER}} --recursive",
                                    } 
                                }
                            }
                        }
                    } }
                })
            });

            resourceStack.WebsiteS3Bucket.GrantWrite(websiteBuildProject.Role);

            var scryfallReaderBuild = new Project(this, "scryfall-reader-build-project", new ProjectProps {
                ProjectName = "scryfall-reader",
                Source = Source.GitHub(new GitHubSourceProps {
                    Owner = "jonasswiatek",
                    Repo = "decklist-api",
                    BranchOrRef = "feature/merge-with-scryfall-import"
                }),
                Environment = new BuildEnvironment {
                    ComputeType = ComputeType.SMALL,
                    BuildImage = LinuxBuildImage.STANDARD_5_0
                },
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>()
                {
                    {"ecr_repo_name", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT, Value = resourceStack.EcrRepo.RepositoryName} },
                    {"ecr_repo_uri", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT, Value = resourceStack.EcrRepo.RepositoryUri} },
                },
                BuildSpec = BuildSpec.FromObject(new Dictionary<string, object> {
                    { "version", "0.2" },
                    { "phases", new Dictionary<string, Dictionary<string, string[]>> {
                        {
                            "pre_build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    {
                                        "PROJECT_NAME=DecklistApi.ScryfallReader",
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
                                        "docker build -t $ecr_repo_uri:$IMAGE_TAG -f ./src/$PROJECT_NAME/Dockerfile ./src/",
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

            resourceStack.EcrRepo.GrantPush(scryfallReaderBuild.Role);
        }
    }
}
