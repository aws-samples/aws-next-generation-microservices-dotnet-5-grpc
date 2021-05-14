using Amazon.CDK;

namespace SubmitOrder
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new SubmitOrderStack(app, "ModernTacoShop-SubmitOrderServiceStack");
            app.Synth();
        }
    }
}
