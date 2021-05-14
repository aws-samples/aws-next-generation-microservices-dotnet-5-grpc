using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SSM;

namespace ModernTacoShop
{
    public class ModernTacoShopStack : Stack
    {
        internal ModernTacoShopStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // create a VPC.
            var vpc = new Vpc(this, "vpc", new VpcProps
            {
                Cidr = "192.168.123.0/24",
                MaxAzs = 3,
                SubnetConfiguration = new SubnetConfiguration[]
                {
                    new SubnetConfiguration
                    {
                        CidrMask = 27,
                        SubnetType = SubnetType.PUBLIC,
                        Name = "Public"
                    },
                    new SubnetConfiguration
                    {
                        CidrMask = 27,
                        SubnetType = SubnetType.PRIVATE,
                        Name = "Private"
                    }
                }
            });

            // Create an S3 bucket to hold the uploaded code.
            var codeBucket = new Bucket(this, "CodeBucket");

            // Store the name of the code bucket in Systems Manager so we can
            // refer to it in other service scripts.
            var codeBucketNameParameter = new StringParameter(this,
                    "ModernTacoShop-CodeBucketNameParameter",
                    new StringParameterProps
                    {
                        ParameterName = "/ModernTacoShop/CodeBucketName",
                        StringValue = codeBucket.BucketName,
                        Tier = ParameterTier.STANDARD
                    }
            );

            new CfnOutput(this, "ModernTacoShop-CodeBucketNameOutput", new CfnOutputProps
            {
                Value = codeBucket.BucketName
            });
        }
    }
}
