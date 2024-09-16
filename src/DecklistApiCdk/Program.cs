using Amazon.CDK;
using DecklistApiCdk;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MtgDecklistsCdk
{
    sealed class Program
    {
        public static readonly string DomainName = "decklist.lol";

        public static readonly string DecklistApiAotImageTag  = "DecklistApi.Web.Aot-2";
        public static readonly string DecklistWebsiteVersion  = "v1.0.1";
        public static readonly string ScryfalllReaderImageTag = "DecklistApi.ScryfallReader-1";

        public static void Main(string[] args)
        {
            var account = "017820661759";
            var app = new App();

            var resourceStack = new ResourceStack(app, "ResourceStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = account,
                    Region = "eu-central-1",
                }
            });

            var use1ResourceStack = new Use1ResourceStack(resourceStack, app, "Use1ResourceStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = account,
                    Region = "us-east-1",
                },
                CrossRegionReferences = true
            });

            use1ResourceStack.AddDependency(resourceStack);

            var buildStack = new BuildStack(resourceStack, app, "BuildStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = account,
                    Region = "eu-central-1",
                }
            });
            
            buildStack.AddDependency(resourceStack);

            var webStack = new WebStack(resourceStack, use1ResourceStack, app, "WebStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = account,
                    Region = "eu-central-1",
                },
                CrossRegionReferences = true
            });

            webStack.AddDependency(resourceStack);
            webStack.AddDependency(use1ResourceStack);
            webStack.AddDependency(buildStack);

            var scryfallStack = new ScryfallStack(resourceStack, app, "ScryfallStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = account,
                    Region = "eu-central-1",
                }
            });

            scryfallStack.AddDependency(resourceStack);

            app.Synth();
        }
    }
}
