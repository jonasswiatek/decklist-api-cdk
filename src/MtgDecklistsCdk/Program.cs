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

            var buildStack = new MtgDecklistsBuildStack(app, "MtgDecklistsBuildStack", new StackProps
            {
                Env = env
            });

            var webStack = new MtgDecklistsWebStack(buildStack.EcrRepo, app, "MtgDecklistsWebStack", new StackProps
            {
                Env = env
            });

            webStack.AddDependency(buildStack);
            app.Synth();
        }
    }
}
