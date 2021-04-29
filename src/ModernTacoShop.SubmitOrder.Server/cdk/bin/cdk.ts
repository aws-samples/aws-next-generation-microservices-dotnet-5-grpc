#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from '@aws-cdk/core';
import { SubmitOrderServiceStack } from '../lib/cdk-stack';

const environment = {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: process.env.CDK_DEFAULT_REGION
};

const app = new cdk.App();
new SubmitOrderServiceStack(app, 'ModernTacoShop-SubmitOrderServiceStack', { env: environment });