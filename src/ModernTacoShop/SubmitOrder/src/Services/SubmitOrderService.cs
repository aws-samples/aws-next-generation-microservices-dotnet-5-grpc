/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * SPDX-License-Identifier: MIT-0
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this
 * software and associated documentation files (the "Software"), to deal in the Software
 * without restriction, including without limitation the rights to use, copy, modify,
 * merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ModernTacoShop.SubmitOrder.Protos;

namespace ModernTacoShop.SubmitOrder.Server
{
    public class SubmitOrderService : Protos.SubmitOrder.SubmitOrderBase
    {
        private readonly ILogger<SubmitOrderService> _logger;
        private string _tableName;

        public SubmitOrderService(ILogger<SubmitOrderService> logger)
        {
            _logger = logger;
        }

        public override Task<Empty> HealthCheck(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> SubmitOrder(Order order, ServerCallContext serverCallContext)
        {
            try
            {
                var systemsManagementClient = new AmazonSimpleSystemsManagementClient();
                if (string.IsNullOrEmpty(_tableName))
                {
                    // The name of the table may vary, so get it from the Systems Manager Parameter Store.
                    var tableNameParameter = await systemsManagementClient.GetParameterAsync(
                        new GetParameterRequest { Name = "/ModernTacoShop/SubmitOrder/OrderTableName" });
                    _tableName = tableNameParameter.Parameter.Value;
                }

                var client = new AmazonDynamoDBClient();
                var context = new DynamoDBContext(client);
                var record = new SubmitOrderDynamoDbRecord(order);
                await context.SaveAsync(record, new DynamoDBOperationConfig() { OverrideTableName = _tableName });


                // Submit the order to the 'Track Order' service.
                // First, get the domain name of the 'Track Order' service from the parameter store.
                var trackOrderServiceDomainNameParameter = await systemsManagementClient.GetParameterAsync(
                    new GetParameterRequest { Name = "/ModernTacoShop/TrackOrder/DomainName" });
                var trackOrderServiceDomainName = trackOrderServiceDomainNameParameter.Parameter.Value;

                using var channel = GrpcChannel.ForAddress($"https://{trackOrderServiceDomainName}");

                var trackOrderClient = new TrackOrder.Protos.TrackOrder.TrackOrderClient(channel);
                var reply = await trackOrderClient.StartTrackingOrderAsync(new TrackOrder.Protos.Order
                {
                    LastPosition = null,
                    LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow),
                    OrderId = order.OrderId,
                    PlacedOn = order.PlacedOn,
                    Status = TrackOrder.Protos.OrderStatus.Placed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SubmitOrder");
            }

            return new Empty();
        }
    }
}
