import * as cdk from '@aws-cdk/core';

import * as ec2 from '@aws-cdk/aws-ec2';
import * as s3 from '@aws-cdk/aws-s3';
import * as ssm from '@aws-cdk/aws-ssm';

export class ModernTacoShopStack extends cdk.Stack {
  constructor(scope: cdk.Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

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
  }
}
