import * as cdk from '@aws-cdk/core';

import * as acm from '@aws-cdk/aws-certificatemanager';
import * as ec2 from '@aws-cdk/aws-ec2';
import * as route53 from '@aws-cdk/aws-route53';
import * as s3 from '@aws-cdk/aws-s3';
import * as ssm from '@aws-cdk/aws-ssm';

export class ModernTacoShopStack extends cdk.Stack {
  constructor(scope: cdk.Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const domainNameParameter = new cdk.CfnParameter(this, 'domainName', {
      type: 'String',
      description: 'The domain name for the tutorial application.'
    });


    // Create a VPC. 
    const vpc = new ec2.Vpc(this, 'vpc', {
      cidr: '192.168.123.0/24',
      maxAzs: 3,
      subnetConfiguration: [
        {
          subnetType: ec2.SubnetType.PUBLIC,
          name: 'Public',
          cidrMask: 27
        },
        {
          subnetType: ec2.SubnetType.PRIVATE,
          name: 'Private',
          cidrMask: 27
        }
      ]
    });

    // Create an S3 bucket to hold the uploaded code.
    const codeBucket = new s3.Bucket(this, 'CodeBucket', {});

    // Store the name of the code bucket in Systems Manager so we can refer to it in other service scripts.
    const codeBucketNameParameter = new ssm.StringParameter(this, 'ModernTacoShop-CodeBucketNameParameter', {
      parameterName: '/ModernTacoShop/CodeBucketName',
      stringValue: codeBucket.bucketName,
      tier: ssm.ParameterTier.STANDARD
    });

    const codeBucketNameOutput = new cdk.CfnOutput(this, 'ModernTacoShop-CodeBucketNameOutput', {
      value: codeBucket.bucketName
    });


    // Create the Route 53 hosted zone.
    const hostedZone = new route53.PublicHostedZone(this, 'HostedZone', {
      zoneName: domainNameParameter.valueAsString
    });

    // Store the name and ID of the hosted zone in Systems Manager, so we can refer to it in other service scripts.
    const hostedZoneNameParameter = new ssm.StringParameter(this, 'ModernTacoShop-HostedZoneNameParameter', {
      parameterName: '/ModernTacoShop/HostedZoneName',
      stringValue: hostedZone.zoneName,
      tier: ssm.ParameterTier.STANDARD
    });
    const hostedZoneIdParameter = new ssm.StringParameter(this, 'ModernTacoShop-HostedZoneIdParameter', {
      parameterName: '/ModernTacoShop/HostedZoneID',
      stringValue: hostedZone.hostedZoneId,
      tier: ssm.ParameterTier.STANDARD
    });

    // Register a certificate for the hosted zone.
    const certificate = new acm.Certificate(this, 'ModernTacoShop-Certificate', {
      domainName: domainNameParameter.valueAsString,
      validation: acm.CertificateValidation.fromDns(hostedZone)
    });

    // Store the certificate ARN in Systems Manager, so we can refer to it in other service scripts.
    const certificateArnParameter = new ssm.StringParameter(this, 'ModernTacoShop-CertificateArnParameter', {
      parameterName: '/ModernTacoShop/CertificateARN',
      stringValue: certificate.certificateArn,
      tier: ssm.ParameterTier.STANDARD
    });

  }
}
