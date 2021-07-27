using Amazon.CDK;

namespace ModernTacoShop.SubmitOrder.Cdk
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new SubmitOrderStack(app, "ModernTacoShop-SubmitOrderStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
                }
            });

            app.Synth();
        }
    }
}
