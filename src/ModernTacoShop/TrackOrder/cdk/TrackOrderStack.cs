using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.AutoScaling;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CodeDeploy;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SSM;

namespace ModernTacoShop.TrackOrder.Cdk
{
    public class TrackOrderStack : Stack
    {
        internal TrackOrderStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // Look up the VPC from another stack.
            var vpc = Vpc.FromLookup(this,
                "ModernTacoShop-VPC",
                new VpcLookupOptions
                {
                    VpcName = "ModernTacoShop-CommonStack/vpc"
                });

            // Get the certificate from Certificate Manager.
            var certificateArnParameter = StringParameter.FromStringParameterAttributes(this,
                "ModernTacoShop-CertificateARNParameter",
                new StringParameterAttributes
                {
                    ParameterName = "/ModernTacoShop/CertificateARN"
                });
            var certificate = Certificate.FromCertificateArn(this,
                "ModernTacoShop-Certificate",
                certificateArnParameter.StringValue);

            // Get the name of the code bucket from the parameter store.
            var codeBucketNameParameter = StringParameter.FromStringParameterAttributes(this,
                "ModernTacoShop-CodeBucketNameParameter",
                new StringParameterAttributes
                {
                    ParameterName = "/ModernTacoShop/CodeBucketName"
                });
            var codeBucket = Bucket.FromBucketName(this,
                "CodeBucket",
                codeBucketNameParameter.StringValue);

            // Create a role for the microservice.
            var trackOrderMicroserviceRole = new Role(this,
                "ModernTacoShop-TrackOrder-MicroserviceRole",
                new RoleProps
                {
                    AssumedBy = new ServicePrincipal("ec2.amazonaws.com")
                });

            // Grant the microservice role access to read from the code bucket.
            // Instances need this access to download the microservice code.
            codeBucket.GrantRead(trackOrderMicroserviceRole);

            // Grant the microservice role full access to CloudWatch logs.
            trackOrderMicroserviceRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"));

            // Grant the microservice role access to read Systems Manager parameters that are microservice domain names.
            var readParameterPolicyArnParameter = StringParameter.FromStringParameterAttributes(this,
                "ModernTacoShop-ReadParameterPolicyARNParameter",
                new StringParameterAttributes
                {
                    ParameterName = "/ModernTacoShop/ReadMicroserviceDomainNameParametersPolicyARN"
                });
            var readParameterPolicy = ManagedPolicy.FromManagedPolicyArn(this,
                "ModernTacoShop-TrackOrder-ReadParameterPolicy",
                readParameterPolicyArnParameter.StringValue);

            trackOrderMicroserviceRole.AddManagedPolicy(readParameterPolicy);

            // Create an instance profile for the role so it can be assigned to EC2 instances.
            var trackOrderMicroserviceInstanceProfile = new CfnInstanceProfile(this,
                "ModernTacoShop-TrackOrder-MicroserviceRole-InstanceProfile",
                new CfnInstanceProfileProps
                {
                    Roles = new string[] { trackOrderMicroserviceRole.RoleName }
                });

            // Create a DynamoDB table for tracked orders.
            var table = new Table(this,
                "ModernTacoShop-TrackOrder-Table",
                new TableProps
                {
                    PartitionKey = new Attribute
                    {
                        Name = "Id",
                        Type = AttributeType.NUMBER
                    },
                    BillingMode = BillingMode.PAY_PER_REQUEST,
                    Encryption = TableEncryption.AWS_MANAGED,
                    PointInTimeRecovery = true,
                    RemovalPolicy = RemovalPolicy.DESTROY
                });

            table.GrantReadWriteData(trackOrderMicroserviceRole);
            table.Grant(trackOrderMicroserviceRole, "dynamodb:DescribeTable");

            // The name of the DynamoDB table is (partially) generated by the script, so store the final table name in Parameter Store.
            var tableNameParameter = new StringParameter(this,
                "ModernTacoShop-TrackOrder-TableNameParameter",
                new StringParameterProps
                {
                    ParameterName = "/ModernTacoShop/TrackOrder/OrderTableName",
                    StringValue = table.TableName,
                    Tier = ParameterTier.STANDARD
                });
            // Allow the microservice role (assumed by the instances) to read the parameter value.
            tableNameParameter.GrantRead(trackOrderMicroserviceRole);

            // Create an ASG to run the service.
            var microserviceSecurityGroup = new SecurityGroup(this,
                "ModernTacoShop-TrackOrder-MicroserviceSecurityGroup",
                new SecurityGroupProps
                {
                    Vpc = vpc,
                    AllowAllOutbound = false
                });

            // Allow outgoing HTTP/S traffic, so the instance can download updates and communicate with AWS regional services
            // (including CodeDeploy and S3).
            microserviceSecurityGroup.AddEgressRule(Peer.AnyIpv4(), Port.Tcp(80));
            microserviceSecurityGroup.AddEgressRule(Peer.AnyIpv4(), Port.Tcp(443));

            // Security: Suppress the cfn_nag warning for the microservice SG allowing outbound HTTP/S.
            ((CfnSecurityGroup)microserviceSecurityGroup.Node.DefaultChild).AddMetadata("cfn_nag",
                    new Dictionary<string, object>
                    {
                        ["rules_to_suppress"] = new Dictionary<string, object>[]
                        {
                            new Dictionary<string, object>
                            {
                                ["id"] = "W5",
                                ["reason"] = "This instance needs outbound Internet access to download updates and communicate with AWS regional services."
                            }
                        }
                    });

            // Use Ubuntu as it is officially supported for .NET 5.
            var ubuntuImage = new LookupMachineImage(new LookupMachineImageProps
            {
                Name = "ubuntu/images/hvm-ssd/ubuntu-focal-20.04-arm64-server-20201026"
            });

            var autoScalingGroup = new AutoScalingGroup(this,
                "ModernTacoShop-TrackOrder-ASG",
                new AutoScalingGroupProps
                {
                    Vpc = vpc,
                    InstanceType = InstanceType.Of(InstanceClass.BURSTABLE4_GRAVITON, InstanceSize.MICRO),
                    MachineImage = ubuntuImage,
                    SecurityGroup = microserviceSecurityGroup,
                    MinCapacity = 1,
                    MaxCapacity = 1,
                    UserData = UserData.ForLinux(),
                    Role = trackOrderMicroserviceRole,
                    VpcSubnets = new SubnetSelection
                    {
                        Subnets = vpc.PrivateSubnets
                    },
                    UpdatePolicy = UpdatePolicy.RollingUpdate(new RollingUpdateOptions
                    {
                        MinInstancesInService = 0
                    }),
                    HealthCheck = Amazon.CDK.AWS.AutoScaling.HealthCheck.Elb(new ElbHealthCheckOptions
                    {
                        Grace = Duration.Minutes(5)
                    }),
                });

            // Create a security group for the load balancer.
            // Set `allowAllOutbound` to false, because this SG should only allow traffic to the microservice SG (which contains the ASG instances).
            // Egress rules will be generated automatically from the configuration below.
            var microserviceLoadBalancerSecurityGroup = new SecurityGroup(this,
                "ModernTacoShop-TrackOrder-MicroserviceLoadBalancerSecurityGroup",
                new SecurityGroupProps
                {
                    Vpc = vpc,
                    AllowAllOutbound = false
                });

            // Security: Suppress the cfn_nag warning for the ALB SG being open to the world.
            // This is a public-facing ELB and Internet ingress should be permitted.
            ((CfnSecurityGroup)microserviceLoadBalancerSecurityGroup.Node.DefaultChild).AddMetadata("cfn_nag",
                    new Dictionary<string, object>
                    {
                        ["rules_to_suppress"] = new Dictionary<string, object>[]
                        {
                            new Dictionary<string, object>
                            {
                                ["id"] = "W2",
                                ["reason"] = "This is a public facing ELB and ingress from the Internet should be permitted."
                            },
                            new Dictionary<string, object>
                            {
                                ["id"] = "W9",
                                ["reason"] = "This is a public facing ELB and ingress from the Internet should be permitted."
                            }
                        }
                    });

            var microserviceLoadBalancer = new ApplicationLoadBalancer(this,
                "ModernTacoShop-TrackOrder-ALB",
                new ApplicationLoadBalancerProps
                {
                    Vpc = vpc,
                    Http2Enabled = true,
                    InternetFacing = true,
                    SecurityGroup = microserviceLoadBalancerSecurityGroup,
                    VpcSubnets = new SubnetSelection
                    {
                        Subnets = vpc.PublicSubnets
                    }
                });

            var loadBalancerAccessLogPrefix = "access-logs/modern-taco-shop/track-order-alb";
            microserviceLoadBalancer.LogAccessLogs(codeBucket, loadBalancerAccessLogPrefix);

            // Create a load balancer listener using HTTPS.
            // HTTPS will terminate at the load balancer, so install the certificate onto the ALB.
            var microserviceListener = microserviceLoadBalancer.AddListener(
                "ModernTacoShop-TrackOrder-ALB-Microservice-Listener",
                new BaseApplicationListenerProps
                {
                    Port = 443,
                    Protocol = ApplicationProtocol.HTTPS,
                    SslPolicy = SslPolicy.TLS12
                });
            microserviceListener.AddCertificates(
                "ModernTacoShop-Certificate",
                new ListenerCertificate[]
                {
                    ListenerCertificate.FromCertificateManager(certificate)
                });

            // Create a target group pointing to the ASG. This will be over HTTP, as SSL is terminated at the load balancer.
            var microserviceTargetGroup = new ApplicationTargetGroup(this,
                "ModernTacoShop-TrackOrder-ASG-Targets",
                new ApplicationTargetGroupProps
                {
                    Protocol = ApplicationProtocol.HTTP,
                    ProtocolVersion = ApplicationProtocolVersion.GRPC,
                    Vpc = vpc,
                    TargetType = TargetType.INSTANCE,
                    Port = 5000,
                    Targets = new IApplicationLoadBalancerTarget[] { autoScalingGroup },
                    DeregistrationDelay = Duration.Seconds(30)
                });

            // Enable a health check on the gRPC service.
            microserviceTargetGroup.ConfigureHealthCheck(new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck()
            {
                Enabled = true,
                Timeout = Duration.Seconds(5),
                HealthyThresholdCount = 2,
                UnhealthyThresholdCount = 2,
                Interval = Duration.Seconds(10),
                Path = "/modern_taco_shop.TrackOrder/HealthCheck",
                HealthyGrpcCodes = "0" // Code `0` in gRPC is success.
            });

            microserviceListener.AddTargetGroups(
                "ModernTacoShop-TrackOrder-ALB-Microservice-Listener-Targets",
                new AddApplicationTargetGroupsProps
                {
                    TargetGroups = new ApplicationTargetGroup[] { microserviceTargetGroup }
                });

            // Create a sub-domain for this microservice, then target that subdomain to the load balancer.
            // First, get the 'parent' hosted zone from parameters that were stored in Parameter Store by the `common` stack.
            var hostedZoneIdParameter = StringParameter.FromStringParameterAttributes(this,
                "ModernTacoShop-ParentHostedZoneID",
                new StringParameterAttributes
                {
                    ParameterName = "/ModernTacoShop/HostedZoneID"
                });
            var hostedZoneNameParameter = StringParameter.FromStringParameterAttributes(this,
                "ModernTacoShop-ParentHostedZoneName",
                new StringParameterAttributes
                {
                    ParameterName = "/ModernTacoShop/HostedZoneName"
                });

            var hostedZone = HostedZone.FromHostedZoneAttributes(this,
                "ModernTacoShop-ParentHostedZone",
                new HostedZoneAttributes
                {
                    HostedZoneId = hostedZoneIdParameter.StringValue,
                    ZoneName = hostedZoneNameParameter.StringValue
                });

            // Create a sub-domain record for this microservice.
            // Target the load balancer.
            var record = new ARecord(this,
                "ModernTacoShop-TrackOrder-ARecord",
                new ARecordProps
                {
                    Zone = hostedZone,
                    Target = RecordTarget.FromAlias(new LoadBalancerTarget(microserviceLoadBalancer)),
                    RecordName = "track-order"
                });

            // Store the service domain name in Systems Manager Parameter Store.
            var recordDomainNameParameter = new StringParameter(this,
                "ModernTacoShop-TrackOrder-DomainNameParameter",
                new StringParameterProps
                {
                    ParameterName = "/ModernTacoShop/TrackOrder/DomainName",
                    StringValue = record.DomainName,
                    Tier = ParameterTier.STANDARD
                });

            // Tag the service domain name parameter so it can be accessed by other microservices.
            Amazon.CDK.Tags.Of(recordDomainNameParameter).Add("parameter-type", "modern-taco-shop-microservice-domain-name");

            // CodeDeploy configuration

            var codeDeployServiceRole = new Role(this,
                "ModernTacoShop-TrackOrder-CodeDeploy-ServiceRole",
                 new RoleProps
                 {
                     AssumedBy = new ServicePrincipal("codedeploy.amazonaws.com")
                 });
            codeDeployServiceRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSCodeDeployRole"));

            var application = new ServerApplication(this,
                "ModernTacoShop-TrackOrder-CodeDeploy-Application",
                new ServerApplicationProps
                {
                    ApplicationName = "ModernTacoShop-TrackOrder"
                });
            var deploymentGroup = new ServerDeploymentGroup(this,
                "ModernTacoShop-TrackOrder-CodeDeploy-DeploymentGroup",
                new ServerDeploymentGroupProps
                {
                    Application = application,
                    LoadBalancer = LoadBalancer.Application(microserviceTargetGroup),
                    AutoScalingGroups = new AutoScalingGroup[] { autoScalingGroup },
                    InstallAgent = true,
                    DeploymentConfig = ServerDeploymentConfig.ALL_AT_ONCE,
                    Role = codeDeployServiceRole
                });

            new CfnOutput(this,
                "ModernTacoShop-TrackOrder-LoadBalancerName",
                new CfnOutputProps
                {
                    Value = microserviceLoadBalancer.LoadBalancerName
                });

            new CfnOutput(this,
                "ModernTacoShop-TrackOrder-DeploymentGroupNameOutput",
                new CfnOutputProps
                {
                    Value = deploymentGroup.DeploymentGroupName
                });
        }
    }
}