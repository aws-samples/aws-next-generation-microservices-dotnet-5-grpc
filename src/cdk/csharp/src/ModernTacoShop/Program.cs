using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernTacoShop
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new ModernTacoShopStack(app, "ModernTacoShop-RootStack");
            app.Synth();
        }
    }
}
