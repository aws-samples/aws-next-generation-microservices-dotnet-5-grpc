using System.Collections.Generic;
using System.Dynamic;
using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SSM;

namespace ModernTacoShop
{
    public class ModernTacoShopStack : Stack
    {
        internal ModernTacoShopStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var publicHostedZoneDomainName = (string)this.Node.TryGetContext("domain-name");

            // Create a VPC.
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
                        Name = "Public",
                    },
                    new SubnetConfiguration
                    {
                        CidrMask = 27,
                        SubnetType = SubnetType.PRIVATE,
                        Name = "Private"
                    }
                }
            });

            // Security: Turn off `MapPublicIpOnLaunch` for public subnets (where the CDK construct enables it by default).
            foreach (var subnet in vpc.PublicSubnets)
            {
                var cfnSubnet = (CfnSubnet)subnet.Node.DefaultChild;
                cfnSubnet.MapPublicIpOnLaunch = false;
            }

            // Security: Suppress the cfn_nag warning for VPC flow logs.
            ((CfnVPC)vpc.Node.DefaultChild).AddMetadata("cfn_nag",
                    new Dictionary<string, object>
                    {
                        ["rules_to_suppress"] = new Dictionary<string, object>[]
                        {
                            new Dictionary<string, object>
                            {
                                ["id"] = "W60",
                                ["reason"] = "VPC flow logs not required in this sample."
                            }
                        }
                    });

            // Create an S3 bucket to hold the uploaded code.
            var codeBucket = new Bucket(this,
                "ModernTacoShop-CodeBucket",
                new BucketProps()
                {
                    AccessControl = BucketAccessControl.LOG_DELIVERY_WRITE,
                    Encryption = BucketEncryption.S3_MANAGED,
                    ServerAccessLogsPrefix = "access-logs/modern-taco-shop/"
                });


            // Allow ELB logs access to the bucket.
            // See: https://docs.aws.amazon.com/elasticloadbalancing/latest/application/load-balancer-access-logs.html#access-logging-bucket-permissions

            var loadBalancerAccessLogPrefix = "access-logs/modern-taco-shop/*";
            var loadBalancerAccessLogResource = $"arn:aws:s3:::{codeBucket.BucketName}/{loadBalancerAccessLogPrefix}/AWSLogs/{this.Account}/*";

            // The ELB account ID is different per region, and there's no API to get it.
            var regionalElbAccountIds = new Dictionary<string, string> {
                { "ap-east-1", "754344448648" },
                { "ap-northeast-1", "582318560864" },
                { "ap-northeast-2", "600734575887" },
                { "ap-northeast-3", "383597477331" },
                { "ap-south-1", "718504428378" },
                { "ap-southeast-1", "114774131450" },
                { "ap-southeast-2", "783225319266" },
                { "ca-central-1", "985666609251" },
                { "cn-north-1", "638102146993" },
                { "cn-northwest-1", "037604701340" },
                { "eu-central-1", "054676820928" },
                { "eu-north-1", "897822967062" },
                { "eu-west-1", "156460612806" },
                { "eu-west-2", "652711504416" },
                { "eu-west-3", "009996457667" },
                { "sa-east-1", "507241528517" },
                { "us-east-1", "127311923021" },
                { "us-east-2", "033677994240" },
                { "us-gov-east-1", "190560391635" },
                { "us-gov-west-1", "048591011584" },
                { "us-west-1", "027434742980" },
                { "us-west-2", "797873946194" },
            };
            var elbAccountId = regionalElbAccountIds[this.Region];

            codeBucket.AddToResourcePolicy(new PolicyStatement(
                new PolicyStatementProps()
                {
                    Actions = new[] { "s3:PutObject" },
                    Effect = Effect.ALLOW,
                    Resources = new[] { loadBalancerAccessLogResource },
                    Principals = new[] { new AccountPrincipal(elbAccountId) }
                }));
            codeBucket.AddToResourcePolicy(new PolicyStatement(
                new PolicyStatementProps()
                {
                    Actions = new[] { "s3:PutObject" },
                    Effect = Effect.ALLOW,
                    Resources = new[] { loadBalancerAccessLogResource },
                    Principals = new[] { new ServicePrincipal("delivery.logs.amazonaws.com") },
                    Conditions = new Dictionary<string, object>
                    {
                        { "StringEquals", new Dictionary<string, object> { { "s3:x-amz-acl", "bucket-owner-full-control" } } }
                    }
                }));
            codeBucket.AddToResourcePolicy(new PolicyStatement(
                new PolicyStatementProps()
                {
                    Actions = new[] { "s3:GetBucketAcl" },
                    Effect = Effect.ALLOW,
                    Resources = new[] { codeBucket.BucketArn },
                    Principals = new[] { new ServicePrincipal("delivery.logs.amazonaws.com") },
                }));

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

            // Get the Route 53 hosted zone that will serve as a 'parent' for the microservices.
            var hostedZone = HostedZone.FromLookup(this,
                "ModernTacoShop-ParentHostedZone",
                new HostedZoneProviderProps()
                {
                    DomainName = publicHostedZoneDomainName
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
                    DomainName = hostedZone.ZoneName,
                    SubjectAlternativeNames = new[] { "*." + hostedZone.ZoneName },
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

            // Create a policy giving access to SSM parameters that contain microservice domain names.
            var policy = new ManagedPolicy(this,
                "ModernTacoShop-ReadMicroserviceDomainNameParametersPolicy",
                new ManagedPolicyProps()
                {
                    Description = "Allows read-only access to SSM parameters tagged as microservice domain names."
                });
            policy.AddStatements(
                new PolicyStatement[]
                    {
                        new PolicyStatement(new PolicyStatementProps
                        {
                            Actions = new string[] { "ssm:GetParameter" },
                            Effect = Effect.ALLOW,
                            Resources = new string[] { $"arn:aws:ssm:{this.Region}:{this.Account}:parameter/*" },
                            Conditions = new Dictionary<string, object>
                            {
                                ["StringEquals"] = new Dictionary<string, string>
                                {
                                    ["aws:ResourceTag/parameter-type"] = "modern-taco-shop-microservice-domain-name"
                                }
                            }
                        })
                    }
                );
            // Store the policy ARN in Systems Manager, so we can refer to it in other service scripts.
            new StringParameter(this,
                "ModernTacoShop-ReadMicroserviceDomainNameParametersPolicyArnParameter",
                new StringParameterProps
                {
                    ParameterName = "/ModernTacoShop/ReadMicroserviceDomainNameParametersPolicyARN",
                    StringValue = policy.ManagedPolicyArn,
                    Tier = ParameterTier.STANDARD
                });

        }
    }
}
