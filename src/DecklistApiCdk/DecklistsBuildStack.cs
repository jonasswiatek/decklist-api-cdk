using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace MtgDecklistsCdk
{
    public class DecklistsBuildStack : Stack
    {
        public Repository EcrRepo;
        public Bucket WebsiteS3Bucket;
        public OriginAccessIdentity WebsiteS3BucketOai;

        internal DecklistsBuildStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
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
                    ComputeType = ComputeType.SMALL,
                    BuildImage = LinuxBuildImage.STANDARD_5_0
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
                                        "PROJECT_NAME=DecklistApi.Web",
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

            EcrRepo.GrantPush(decklistApiBuild.Role);

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

            var websiteBuildProject = new Project(this, "decklist-api-website-build", new ProjectProps {
                ProjectName = "decklist-website",
                Source = Source.GitHub(new GitHubSourceProps {
                    Owner = "jonasswiatek",
                    Repo = "decklist-website",
                }),
                Environment = new BuildEnvironment {
                    ComputeType = ComputeType.SMALL,
                    BuildImage = LinuxBuildImage.STANDARD_5_0
                },
                BuildSpec = BuildSpec.FromObject(new Dictionary<string, object> {
                    { "version", "0.2" },
                    { "phases", new Dictionary<string, Dictionary<string, string[]>> {
                        {
                            "pre_build", new Dictionary<string, string[]>
                            {
                                { "Commands", new []
                                    {
                                        "n 22",
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
                                        $"aws s3 cp ./dist s3://{WebsiteS3Bucket.BucketName}/v1.0.${{CODEBUILD_BUILD_NUMBER}} --recursive",
                                    } 
                                }
                            }
                        }
                    } }
                })
            });

            WebsiteS3Bucket.GrantRead(WebsiteS3BucketOai);
            WebsiteS3Bucket.GrantWrite(websiteBuildProject.Role);
        }
    }
}
