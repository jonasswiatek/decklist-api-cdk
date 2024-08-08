using Amazon.CDK;
using DecklistApiCdk;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MtgDecklistsCdk
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            var env = new Amazon.CDK.Environment
            {
                Account = "017820661759",
                Region = "eu-central-1",
            };

            var resourceStack = new ResourceStack(app, "ResourceStack", new StackProps
            {
                Env = env
            });

            var buildStack = new DecklistsBuildStack(resourceStack, app, "BuildStack", new StackProps
            {
                Env = env
            });
            
            buildStack.AddDependency(resourceStack);

            var webStack = new DecklistsWebStack(resourceStack, app, "WebStack", new StackProps
            {
                Env = env
            });

            webStack.AddDependency(resourceStack);
            webStack.AddDependency(buildStack);

            app.Synth();
        }
    }
}
