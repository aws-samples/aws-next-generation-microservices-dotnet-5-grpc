import * as cdk from '@aws-cdk/core';

import * as autoscaling from '@aws-cdk/aws-autoscaling';
import * as acm from '@aws-cdk/aws-certificatemanager';
import * as codedeploy from '@aws-cdk/aws-codedeploy';
import * as dynamodb from '@aws-cdk/aws-dynamodb';
import * as ec2 from '@aws-cdk/aws-ec2';
import * as elb from '@aws-cdk/aws-elasticloadbalancingv2';
import * as iam from '@aws-cdk/aws-iam';
import * as route53 from '@aws-cdk/aws-route53';
import * as route53_targets from '@aws-cdk/aws-route53-targets';
import * as s3 from '@aws-cdk/aws-s3';
import * as ssm from '@aws-cdk/aws-ssm';

export class TrackOrderServiceStack extends cdk.Stack {
    constructor(scope: cdk.Construct, id: string, props?: cdk.StackProps) {
        super(scope, id, props);

        const keyPairName = new cdk.CfnParameter(this, 'keyPairName', {
            type: 'String',
            description: 'The name of the EC2 keypair associated with the Windows Server instance.'
        });

        // Look up the VPC from another stack.
        const vpc = ec2.Vpc.fromLookup(this, 'ModernTacoShop-VPC', {
            vpcName: 'ModernTacoShop-RootStack/vpc'
        });

        // Get the certificate from Certificate Manager.
        const certificateARN = ssm.StringParameter.fromStringParameterAttributes(this, 'ModernTacoShop-CertificateARNParameter', {
            parameterName: '/ModernTacoShop/CertificateARN'
        }).stringValue;
        const certificate = acm.Certificate.fromCertificateArn(this, 'ModernTacoShop-Certificate', certificateARN);

        // Get the name of the code bucket from the parameter store.
        const codeBucketName = ssm.StringParameter.fromStringParameterAttributes(this, 'ModernTacoShop-CodeBucketNameParameter', {
            parameterName: '/ModernTacoShop/CodeBucketName'
        }).stringValue;
        const codeBucket = s3.Bucket.fromBucketName(this, 'CodeBucket', codeBucketName);

        // Create a role for the microservice.
        const trackOrderMicroserviceRole = new iam.Role(this, 'ModernTacoShop-TrackOrder-MicroserviceRole', {
            assumedBy: new iam.ServicePrincipal('ec2.amazonaws.com')
        });

        // Grant the microservice role access to read from the code bucket.
        // Instances need this access to download the microservice code.
        codeBucket.grantRead(trackOrderMicroserviceRole);

        // Grant the microservice role full access to CloudWatch logs. 
        trackOrderMicroserviceRole.addManagedPolicy({ managedPolicyArn: 'arn:aws:iam::aws:policy/CloudWatchLogsFullAccess' });

        // Grant the microservice role access to read Systems Manager parameters that are microservice domain names.
        trackOrderMicroserviceRole.addToPolicy(new iam.PolicyStatement({
            actions: ['ssm:GetParameter'],
            effect: iam.Effect.ALLOW,
            resources: ['*'],
            conditions: {
                StringEquals: { "aws:ResourceTag/parameter-type": "microservice-domain-name" }
            }
        }));

        // Create an instance profile for the role so it can be assigned to EC2 instances.
        const trackOrderMicroserviceInstanceProfile = new iam.CfnInstanceProfile(this, 'ModernTacoShop-TrackOrder-MicroserviceRole-InstanceProfile', {
            roles: [trackOrderMicroserviceRole.roleName]
        });


        // Create a DynamoDB table for tracked orders.
        const table = new dynamodb.Table(this, 'ModernTacoShop-TrackOrder-Table', {
            partitionKey: { name: 'id', type: dynamodb.AttributeType.NUMBER }
        });
        table.grantReadWriteData(trackOrderMicroserviceRole);
        table.grant(trackOrderMicroserviceRole, 'dynamodb:DescribeTable');

        // The name of the DynamoDB table is (partially) generated by the script, so store the final table name in Parameter Store.
        const tableNameParameter = new ssm.StringParameter(this, 'ModernTacoShop-TrackOrder-TableNameParameter', {
            parameterName: '/ModernTacoShop/TrackOrder/OrderTableName',
            stringValue: table.tableName,
            tier: ssm.ParameterTier.STANDARD
        });
        // Allow the microservice role (assumed by the instances) to read the parameter value.
        tableNameParameter.grantRead(trackOrderMicroserviceRole);


        // Create an ASG to run the service.
        const microserviceSecurityGroup = new ec2.SecurityGroup(this, 'ModernTacoShop-TrackOrder-MicroserviceSecurityGroup', {
            vpc: vpc
        });

        // Use Ubuntu as it is officially supported for .NET 5.
        const ubuntuImage = new ec2.LookupMachineImage({
            name: 'ubuntu/images/hvm-ssd/ubuntu-focal-20.04-arm64-server-20201026'
        });

        const autoScalingGroup = new autoscaling.AutoScalingGroup(this, 'ModernTacoShop-TrackOrder-ASG', {
            vpc,
            instanceType: ec2.InstanceType.of(ec2.InstanceClass.BURSTABLE4_GRAVITON, ec2.InstanceSize.MICRO),
            machineImage: ubuntuImage,
            securityGroup: microserviceSecurityGroup,
            minCapacity: 1,
            maxCapacity: 1,
            userData: ec2.UserData.forLinux(),
            role: trackOrderMicroserviceRole,
            vpcSubnets: { subnets: vpc.privateSubnets },
            keyName: keyPairName.valueAsString,
            updatePolicy: autoscaling.UpdatePolicy.rollingUpdate({ minInstancesInService: 0 }),
            healthCheck: autoscaling.HealthCheck.ec2()
        });

        // Create a security group for the load balancer. 
        // Set`allowAllOutbound` to false because this SG should only allow traffic to the microservice SG (which contains the ASG instances).
        // This egress rule will be generated automatically from the configuration below.
        const microserviceLoadBalancerSecurityGroup = new ec2.SecurityGroup(this, 'ModernTacoShop-TrackOrder-MicroserviceLoadBalancerSecurityGroup', {
            vpc: vpc,
            allowAllOutbound: false
        });
        const microserviceLoadBalancer = new elb.ApplicationLoadBalancer(this, 'ModernTacoShop-TrackOrder-ALB', {
            vpc: vpc,
            http2Enabled: true,
            internetFacing: true,
            securityGroup: microserviceLoadBalancerSecurityGroup,
            vpcSubnets: { subnets: vpc.publicSubnets }
        });

        //  Create a load balancer listener. Use HTTPS, as gRPC requires it.
        const microserviceListener = microserviceLoadBalancer.addListener('ModernTacoShop-TrackOrder-ALB-Microservice-Listener', {
            port: 443,
            protocol: elb.ApplicationProtocol.HTTPS
        });
        microserviceListener.addCertificates('ModernTacoShop-Certificate', [certificate]);

        // Create a target group pointing to the ASG. This can be over HTTP -- we can terminate SSL at the load balancer.
        const microserviceTargetGroup = new elb.ApplicationTargetGroup(this, 'ModernTacoShop-TrackOrder-ASG-Targets', {
            protocol: elb.ApplicationProtocol.HTTP,
            vpc: vpc,
            targetType: elb.TargetType.INSTANCE,
            port: 5000,
            targets: [autoScalingGroup]
        });

        // The gRPC protocol version isn't yet supported by the CDK or CFN wrappers, so set it as a raw property.
        const cfnTargetGroup = microserviceTargetGroup.node.defaultChild as elb.CfnTargetGroup;
        cfnTargetGroup.addPropertyOverride('ProtocolVersion', 'GRPC');

        // Some of the CDK validation for health checks conflicts with gRPC, so use the raw properties to customize them.
        cfnTargetGroup.addPropertyOverride('HealthCheckPath', '/modern_taco_shop.TrackOrder/HealthCheck');

        // Use a result code of 0 (OK) for successful health checks, because the service is specifically coded 
        // to return success in the HealthCheck method. The default value is 12 (Unimplemented).
        cfnTargetGroup.addPropertyOverride('Matcher', { "GrpcCode": "0" });


        microserviceListener.addTargetGroups('ModernTacoShop-TrackOrder-ALB-Microservice-Listener-Targets', {
            targetGroups: [microserviceTargetGroup]
        });


        // Add the load balancer to the Route 53 hosted zone.

        // Get the hosted zone parameters from Systems Manager.
        const hostedZoneId = ssm.StringParameter.fromStringParameterAttributes(this, 'ModernTacoShop-RootHostedZoneID', {
            parameterName: '/ModernTacoShop/HostedZoneID'
        }).stringValue;
        const hostedZoneName = ssm.StringParameter.fromStringParameterAttributes(this, 'ModernTacoShop-RootHostedZoneName', {
            parameterName: '/ModernTacoShop/HostedZoneName'
        }).stringValue;

        const hostedZone = route53.HostedZone.fromHostedZoneAttributes(this, 'ModernTacoShop-RootHostedZone', {
            hostedZoneId: hostedZoneId,
            zoneName: hostedZoneName
        });

        const record = new route53.ARecord(this, 'ModernTacoShop-TrackOrder-ARecord', {
            zone: hostedZone,
            target: route53.RecordTarget.fromAlias(new route53_targets.LoadBalancerTarget(microserviceLoadBalancer)),
            recordName: 'track-order'
        });

        // Store the service domain name in Systems Manager Parameter Store.
        const recordDomainNameParameter = new ssm.StringParameter(this, 'ModernTacoShop-TrackOrder-DomainNameParameter', {
            parameterName: '/ModernTacoShop/TrackOrder/DomainName',
            stringValue: record.domainName,
            tier: ssm.ParameterTier.STANDARD
        });

        // Tag the service domain name parameter so it can be accessed by other microservices.
        cdk.Tags.of(recordDomainNameParameter).add('parameter-type', 'microservice-domain-name');


        // CodeDeploy configuration

        const codeDeployServiceRole = new iam.Role(this, 'codedeploy-service-role', {
            assumedBy: new iam.ServicePrincipal('codedeploy.amazonaws.com')
        });
        codeDeployServiceRole.addManagedPolicy({ managedPolicyArn: 'arn:aws:iam::aws:policy/service-role/AWSCodeDeployRole' });

        const application = new codedeploy.ServerApplication(this, 'codedeploy-application', {
            applicationName: 'ModernTacoShop-TrackOrder'
        });
        const deploymentGroup = new codedeploy.ServerDeploymentGroup(this, 'codedeploy-deploymentgroup', {
            application: application,
            loadBalancer: codedeploy.LoadBalancer.application(microserviceTargetGroup),
            autoScalingGroups: [autoScalingGroup],
            installAgent: true,
            deploymentConfig: codedeploy.ServerDeploymentConfig.ALL_AT_ONCE,
            role: codeDeployServiceRole
        });

        const deploymentGroupNameOutput = new cdk.CfnOutput(this, 'ModernTacoShop-TrackOrder-DeploymentGroupNameOutput', {
            value: deploymentGroup.deploymentGroupName
        });
    }
}

