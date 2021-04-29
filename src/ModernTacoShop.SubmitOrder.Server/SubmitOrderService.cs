using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ModernTacoShop.SubmitOrder.Protos;

namespace ModernTacoShop.SubmitOrder.Server
{
    public class SubmitOrderService : ModernTacoShop.SubmitOrder.Protos.SubmitOrder.SubmitOrderBase
    {
        private readonly ILogger<SubmitOrderService> _logger;

        private readonly AmazonDynamoDBClient _dynamoDBClient;
        private readonly AmazonSimpleSystemsManagementClient _systemsManagementClient;

        private Table _orderTable;

        public SubmitOrderService(ILogger<SubmitOrderService> logger)
        {
            _logger = logger;

            // Initialize AWS clients.
            _systemsManagementClient = new AmazonSimpleSystemsManagementClient();
            _dynamoDBClient = new AmazonDynamoDBClient();
        }

        private async Task InitializeTableAsync()
        {
            // The name of the table may vary, so get it from the Systems Manager Parameter Store.
            var tableNameParameter = await _systemsManagementClient.GetParameterAsync(
                new GetParameterRequest { Name = "/ModernTacoShop/SubmitOrder/OrderTableName" });
            var tableName = tableNameParameter.Parameter.Value;

            if (_orderTable == null)
                _orderTable = Table.LoadTable(_dynamoDBClient, tableName);
        }

        public override Task<Empty> HealthCheck(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> SubmitOrder(Order request, ServerCallContext context)
        {
            try
            {
                await this.InitializeTableAsync();

                var orderPlacedOn = DateTime.UtcNow;

                // Write the order to DynamoDB.
                var orderDocument = new Document(new Dictionary<string, DynamoDBEntry>
                {
                    ["id"] = request.OrderId,
                    ["orderJson"] = request.OrderJson,
                    ["placedOn"] = orderPlacedOn
                });

                await _orderTable.PutItemAsync(orderDocument);

                // Get the domain name of the 'Track Order' service from the parameter store.
                var trackOrderServiceDomainNameParameter = await _systemsManagementClient.GetParameterAsync(
                    new GetParameterRequest { Name = "/ModernTacoShop/TrackOrder/DomainName" });
                var trackOrderServiceDomainName = trackOrderServiceDomainNameParameter.Parameter.Value;

                // Submit the order to the 'Track Order' service.
                using var channel = GrpcChannel.ForAddress($"https://{trackOrderServiceDomainName}");
                var client = new ModernTacoShop.TrackOrder.Protos.TrackOrder.TrackOrderClient(channel);
                var reply = await client.StartTrackingOrderAsync(new TrackOrder.Protos.Order
                {
                    LastUpdated = Timestamp.FromDateTime(orderPlacedOn),
                    LastUpdatedPosition = new TrackOrder.Protos.NullablePoint() { Null = NullValue.NullValue },
                    OrderId = request.OrderId,
                    OrderPlaced = Timestamp.FromDateTime(orderPlacedOn),
                    OrderJson = request.OrderJson,
                    OrderStatus = TrackOrder.Protos.OrderStatus.Placed
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
