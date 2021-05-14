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
using System.Collections.Generic;

namespace TrackOrder
{
    public class TrackOrderStack : Stack
    {
        internal TrackOrderStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var keyPairName = new Amazon.CDK.CfnParameter(this, "keyPairName", new Amazon.CDK.CfnParameterProps
            {
                Type = "String",
                Description = "The name of the EC2 keypair associated with the Windows Server instance."
            });

            // Look up the VPC from another stack.
            var vpc = Vpc.FromLookup(this, "ModernTacoShop-VPC", new VpcLookupOptions
            {
                VpcName = "ModernTacoShop-RootStack/vpc"
            });

            // Get the certificate from Certificate Manager.
            var certificateARN = StringParameter.FromStringParameterAttributes(this, "ModernTacoShop-CertificateARNParameter", new StringParameterAttributes
            {
                ParameterName = "/ModernTacoShop/CertificateARN"
            }).StringValue;
            var certificate = Certificate.FromCertificateArn(this, "ModernTacoShop-Certificate", certificateARN);

            // Get the name of the code bucket from the parameter store.
            var codeBucketName = StringParameter.FromStringParameterAttributes(this, "ModernTacoShop-CodeBucketNameParameter", new StringParameterAttributes
            {
                ParameterName = "/ModernTacoShop/CodeBucketName"
            }).StringValue;
            var codeBucket = Bucket.FromBucketName(this, "CodeBucket", codeBucketName);

            // Create a role for the microservice.
            var trackOrderMicroserviceRole = new Role(this, "ModernTacoShop-TrackOrder-MicroserviceRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ec2.amazonaws.com")
            });

            // Grant the microservice role access to read from the code bucket.
            // Instances need this access to download the microservice code.
            codeBucket.GrantRead(trackOrderMicroserviceRole);

            // Grant the microservice role full access to CloudWatch logs.
            trackOrderMicroserviceRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"));

            // Grant the microservice role access to read Systems Manager parameters that are microservice domain names.
            trackOrderMicroserviceRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = new string[] { "ssm:GetParameter" },
                Effect = Effect.ALLOW,
                Resources = new string[] { "*" },
                Conditions = new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, string>
                    {
                        ["aws:ResourceTag/parameter-type"] = "microservice-domain-name"
                    }
                }
            }));

            // Create an instance profile for the role so it can be assigned to EC2 instances.
            var trackOrderMicroserviceInstanceProfile = new CfnInstanceProfile(this, "ModernTacoShop-TrackOrder-MicroserviceRole-InstanceProfile", new CfnInstanceProfileProps
            {
                Roles = new string[] { trackOrderMicroserviceRole.RoleName }
            });

            // Create a DynamoDB table for tracked orders.
            var table = new Table(this, "ModernTacoShop-TrackOrder-Table", new TableProps
            {
                PartitionKey = new Attribute
                {
                    Name = "id",
                    Type = AttributeType.NUMBER
                }
            });
            table.GrantReadWriteData(trackOrderMicroserviceRole);
            table.Grant(trackOrderMicroserviceRole, "dynamodb:DescribeTable");

            // The name of the DynamoDB table is (partially) generated by the script, so store the final table name in Parameter Store.
            var tableNameParameter = new StringParameter(this, "ModernTacoShop-TrackOrder-TableNameParameter", new StringParameterProps
            {
                ParameterName = "/ModernTacoShop/TrackOrder/OrderTableName",
                StringValue = table.TableName,
                Tier = ParameterTier.STANDARD
            });
            // Allow the microservice role (assumed by the instances) to read the parameter value.
            tableNameParameter.GrantRead(trackOrderMicroserviceRole);

            // Create an ASG to run the service.
            var microserviceSecurityGroup = new SecurityGroup(this, "ModernTacoShop-TrackOrder-MicroserviceSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc
            });

            // Use Ubuntu as it is officially supported for .NET 5.
            var ubuntuImage = new LookupMachineImage(new LookupMachineImageProps
            {
                Name = "ubuntu/images/hvm-ssd/ubuntu-focal-20.04-arm64-server-20201026"
            });

            var autoScalingGroup = new AutoScalingGroup(this, "ModernTacoShop-TrackOrder-ASG", new AutoScalingGroupProps
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
                KeyName = keyPairName.ValueAsString,
                UpdatePolicy = UpdatePolicy.RollingUpdate(new RollingUpdateOptions
                {
                    MinInstancesInService = 0
                }),
                HealthCheck = Amazon.CDK.AWS.AutoScaling.HealthCheck.Ec2()
            });

            // Create a security group for the load balancer.
            // Set`allowAllOutbound` to false because this SG should only allow traffic to the microservice SG (which contains the ASG instances).
            // This egress rule will be generated automatically from the configuration below.
            var microserviceLoadBalancerSecurityGroup = new SecurityGroup(this, "ModernTacoShop-TrackOrder-MicroserviceLoadBalancerSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                AllowAllOutbound = false
            });
            var microserviceLoadBalancer = new ApplicationLoadBalancer(this, "ModernTacoShop-TrackOrder-ALB", new ApplicationLoadBalancerProps
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

            //  Create a load balancer listener. Use HTTPS, as gRPC requires it.
            var microserviceListener = microserviceLoadBalancer.AddListener("ModernTacoShop-TrackOrder-ALB-Microservice-Listener", new BaseApplicationListenerProps
            {
                Port = 443,
                Protocol = ApplicationProtocol.HTTPS
            });
            microserviceListener.AddCertificates("ModernTacoShop-Certificate", new ListenerCertificate[]
            {
                ListenerCertificate.FromCertificateManager(certificate)
            });

            // Create a target group pointing to the ASG. This can be over HTTP -- we can terminate SSL at the load balancer.
            var microserviceTargetGroup = new ApplicationTargetGroup(this, "ModernTacoShop-TrackOrder-ASG-Targets", new ApplicationTargetGroupProps
            {
                Protocol = ApplicationProtocol.HTTP,
                Vpc = vpc,
                TargetType = TargetType.INSTANCE,
                Port = 5000,
                Targets = new IApplicationLoadBalancerTarget[] { autoScalingGroup }
            });

            // The gRPC protocol version isn't yet supported by the CDK or CFN wrappers, so set it as a raw property.
            var cfnTargetGroup = microserviceTargetGroup.Node.DefaultChild as CfnTargetGroup;
            cfnTargetGroup.AddPropertyOverride("ProtocolVersion", "GRPC");

            // Some of the CDK validation for health checks conflicts with gRPC, so use the raw properties to customize them.
            cfnTargetGroup.AddPropertyOverride("HealthCheckPath", "/modern_taco_shop.TrackOrder/HealthCheck");

            // Use a result code of 0 (OK) for successful health checks, because the service is specifically coded
            // to return success in the HealthCheck method. The default value is 12 (Unimplemented).
            cfnTargetGroup.AddPropertyOverride("Matcher", new { GrpcCode = 0 });

            microserviceListener.AddTargetGroups("ModernTacoShop-TrackOrder-ALB-Microservice-Listener-Targets", new AddApplicationTargetGroupsProps
            {
                TargetGroups = new ApplicationTargetGroup[] { microserviceTargetGroup }
            });

            // Add the load balancer to the Route 53 hosted zone.

            // Get the hosted zone parameters from Systems Manager.
            var hostedZoneId = StringParameter.FromStringParameterAttributes(this, "ModernTacoShop-RootHostedZoneID", new StringParameterAttributes
            {
                ParameterName = "/ModernTacoShop/HostedZoneID"
            }).StringValue;
            var hostedZoneName = StringParameter.FromStringParameterAttributes(this, "ModernTacoShop-RootHostedZoneName", new StringParameterAttributes
            {
                ParameterName = "/ModernTacoShop/HostedZoneName"
            }).StringValue;

            var hostedZone = HostedZone.FromHostedZoneAttributes(this, "ModernTacoShop-RootHostedZone", new HostedZoneAttributes
            {
                HostedZoneId = hostedZoneId,
                ZoneName = hostedZoneName
            });

            var record = new ARecord(this, "ModernTacoShop-TrackOrder-ARecord", new ARecordProps
            {
                Zone = hostedZone,
                Target = RecordTarget.FromAlias(new LoadBalancerTarget(microserviceLoadBalancer)),
                RecordName = "track-order"
            });

            // Store the service domain name in Systems Manager Parameter Store.
            var recordDomainNameParameter = new StringParameter(this, "ModernTacoShop-TrackOrder-DomainNameParameter", new StringParameterProps
            {
                ParameterName = "/ModernTacoShop/TrackOrder/DomainName",
                StringValue = record.DomainName,
                Tier = ParameterTier.STANDARD
            });

            // Tag the service domain name parameter so it can be accessed by other microservices.
            Amazon.CDK.Tags.Of(recordDomainNameParameter).Add("parameter-type", "microservice-domain-name");

            // CodeDeploy configuration

            var codeDeployServiceRole = new Role(this, "codedeploy-service-role", new RoleProps
            {
                AssumedBy = new ServicePrincipal("codedeploy.amazonaws.com")
            });
            codeDeployServiceRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSCodeDeployRole"));

            var application = new ServerApplication(this, "codedeploy-application", new ServerApplicationProps
            {
                ApplicationName = "ModernTacoShop-TrackOrder"
            });
            var deploymentGroup = new ServerDeploymentGroup(this, "codedeploy-deploymentgroup", new ServerDeploymentGroupProps
            {
                Application = application,
                LoadBalancer = LoadBalancer.Application(microserviceTargetGroup),
                AutoScalingGroups = new AutoScalingGroup[] { autoScalingGroup },
                InstallAgent = true,
                DeploymentConfig = ServerDeploymentConfig.ALL_AT_ONCE,
                Role = codeDeployServiceRole
            });

            var deploymentGroupNameOutput = new CfnOutput(this, "ModernTacoShop-TrackOrder-DeploymentGroupNameOutput", new CfnOutputProps
            {
                Value = deploymentGroup.DeploymentGroupName
            });
        }
    }
}
