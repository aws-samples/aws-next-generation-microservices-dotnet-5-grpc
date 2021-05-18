using Amazon.CDK;

namespace TrackOrder
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new TrackOrderStack(app, "CsharpStack");
            app.Synth();
        }
    }
}
