using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SSM;

namespace ModernTacoShop
{
    public class ModernTacoShopStack : Stack
    {
        internal ModernTacoShopStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var domainNameParameter = new Amazon.CDK.CfnParameter(this,
                "domainName",
                 new Amazon.CDK.CfnParameterProps
                 {
                     Type = "String",
                     Description = "The domain name for the tutorial application."
                 });

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

            // Create the Route 53 hosted zone.
            var hostedZone = new PublicHostedZone(this,
                "ModernTacoShop-HostedZone",
                new PublicHostedZoneProps
                {
                    ZoneName = domainNameParameter.ValueAsString
                });

            // Store the name and ID of the hosted zone in Systems Manager, so we can refer to it in other service scripts.
            new StringParameter(this,
                "ModernTacoShop-HostedZoneNameParameter",
                new StringParameterProps
                {
                    ParameterName = "/ModernTacoShop/HostedZoneName",
                    StringValue = hostedZone.ZoneName,
                    Tier = ParameterTier.STANDARD
                });
            new StringParameter(this,
                "ModernTacoShop-HostedZoneIdParameter",
                new StringParameterProps
                {
                    ParameterName = "/ModernTacoShop/HostedZoneID",
                    StringValue = hostedZone.HostedZoneId,
                    Tier = ParameterTier.STANDARD
                });

            // Register a certificate for the hosted zone.
            var certificate = new Certificate(this,
                "ModernTacoShop-Certificate",
                new CertificateProps
                {
                    DomainName = domainNameParameter.ValueAsString,
                    Validation = CertificateValidation.FromDns(hostedZone)
                });

            // Store the certificate ARN in Systems Manager, so we can refer to it in other service scripts.
            new StringParameter(this,
                "ModernTacoShop-CertificateArnParameter",
                new StringParameterProps
                {
                    ParameterName = "/ModernTacoShop/CertificateARN",
                    StringValue = certificate.CertificateArn,
                    Tier = ParameterTier.STANDARD
                });
        }
    }
}
