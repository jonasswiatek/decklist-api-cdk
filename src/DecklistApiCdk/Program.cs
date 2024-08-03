using Amazon.CDK;
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

            var buildStack = new DecklistsBuildStack(app, "DecklistsBuildStack", new StackProps
            {
                Env = env
            });

            var webStack = new DecklistsWebStack(buildStack.EcrRepo, app, "DecklistsWebStack", new StackProps
            {
                Env = env
            });

            webStack.AddDependency(buildStack);
            app.Synth();
        }
    }
}
