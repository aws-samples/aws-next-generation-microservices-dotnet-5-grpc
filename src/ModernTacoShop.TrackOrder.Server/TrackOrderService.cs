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
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ModernTacoShop.TrackOrder.Protos;
using Microsoft.Extensions.Logging;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;
using System.Collections.Generic;
using System.Threading;

namespace ModernTacoShop.TrackOrder.Server
{
    public class TrackOrderService : ModernTacoShop.TrackOrder.Protos.TrackOrder.TrackOrderBase
    {
        private readonly ILogger<TrackOrderService> _logger;

        private readonly AmazonDynamoDBClient _dynamoDBClient;
        private readonly AmazonSimpleSystemsManagementClient _systemsManagementClient;

        private Table _orderTable;

        // Constants for the latitude & longitude coordinates of the restaurant (where orders start their transit).
        private const decimal _restaurantLatitude = 47.623211m;
        private const decimal _restaurantLongitude = -122.337158m;

        public TrackOrderService(ILogger<TrackOrderService> logger)
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
                new GetParameterRequest { Name = "/ModernTacoShop/TrackOrder/OrderTableName" });
            var tableName = tableNameParameter.Parameter.Value;

            if (_orderTable == null)
                _orderTable = Table.LoadTable(_dynamoDBClient, tableName);
        }

        private async Task SaveOrderAsync(Order order)
        {
            // Write the order to DynamoDB.
            var orderDocument = new Document(new Dictionary<string, DynamoDBEntry>
            {
                ["id"] = order.OrderId,
                ["lastUpdatedOn"] = DateTime.UtcNow,
                ["orderJson"] = order.OrderJson,
                ["placedOn"] = order.OrderPlaced.ToDateTime().ToUniversalTime(),
                ["orderStatus"] = System.Enum.GetName(order.OrderStatus)
            });

            if (order.LastUpdatedPosition.Point == null)
                orderDocument["lastUpdatedPosition_Lat"] = orderDocument["lastUpdatedPosition_Long"] = "";
            else
            {
                orderDocument["lastUpdatedPosition_Lat"] = order.LastUpdatedPosition.Point.Latitude;
                orderDocument["lastUpdatedPosition_Long"] = order.LastUpdatedPosition.Point.Longitude;
            }

            await _orderTable.UpdateItemAsync(orderDocument);
        }

        public override Task<Empty> HealthCheck(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> StartTrackingOrder(Order request, ServerCallContext context)
        {
            try
            {
                await this.InitializeTableAsync();
                await this.SaveOrderAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TrackOrder");
            }
            return new Empty();
        }

        public override async Task GetOrderStatus(OrderId request, IServerStreamWriter<Order> responseStream, ServerCallContext context)
        {
            try
            {
                await this.InitializeTableAsync();

                var dynamoDBItem = await _orderTable.GetItemAsync(request.Id);

                // Load the record from DynamoDB.
                var order = new Order()
                {
                    LastUpdated = Timestamp.FromDateTime(dynamoDBItem["lastUpdatedOn"].AsDateTime().ToUniversalTime()),
                    LastUpdatedPosition = new NullablePoint { Null = NullValue.NullValue },
                    OrderId = dynamoDBItem["id"].AsLong(),
                    OrderJson = dynamoDBItem["orderJson"].AsString(),
                    OrderPlaced = Timestamp.FromDateTime(dynamoDBItem["placedOn"].AsDateTime().ToUniversalTime()),
                    OrderStatus = System.Enum.Parse<OrderStatus>(dynamoDBItem["orderStatus"].AsString())
                };

                order.LastUpdatedPosition = new NullablePoint();
                if (dynamoDBItem.ContainsKey("lastUpdatedPosition_Lat") && dynamoDBItem["lastUpdatedPosition_Lat"].AsDynamoDBNull() != null)
                {
                    order.LastUpdatedPosition.Point = new Point()
                    {
                        Latitude = dynamoDBItem["lastUpdatedPosition_Lat"].AsString(),
                        Longitude = dynamoDBItem["lastUpdatedPosition_Long"].AsString()
                    };
                }

                // Simulate an order being tracked. Update the position every few seconds.

                var maximum = 30; // 60 seconds * 5 minutes
                var i = 0;
                var random = new Random();

                while (!context.CancellationToken.IsCancellationRequested && i <= maximum)
                {
                    switch (i)
                    {
                        case var _ when i < 5:
                            order.OrderStatus = OrderStatus.Preparing;
                            break;

                        case var _ when i >= 5 && i < maximum:
                            order.OrderStatus = OrderStatus.InTransit;

                            if (order.LastUpdatedPosition.Point == null)
                            {
                                // We don't have a position. Initialize it with the coordinates of the restaurant.
                                order.LastUpdatedPosition = new NullablePoint { Point = new Point() };
                                order.LastUpdatedPosition.Point.Latitude = _restaurantLatitude.ToString();
                                order.LastUpdatedPosition.Point.Longitude = _restaurantLongitude.ToString();
                            }
                            else
                            {
                                // Simulate updates to the position with realistic-ish (but fake) motion.

                                var latitudeValue = Decimal.Parse(order.LastUpdatedPosition.Point.Latitude);
                                var longitudeValue = Decimal.Parse(order.LastUpdatedPosition.Point.Longitude);

                                var latitudeIncrement = new Decimal(random.Next(5, 20)) / 100000;
                                var longitudeIncrement = -1 * new Decimal(random.Next(5, 20)) / 100000;

                                order.LastUpdatedPosition.Point.Latitude = (latitudeValue + latitudeIncrement).ToString();
                                order.LastUpdatedPosition.Point.Longitude = (longitudeValue + longitudeIncrement).ToString();
                            }
                            break;

                        case var _ when i == maximum:
                            // The order has been delivered.
                            order.OrderStatus = OrderStatus.Delivered;
                            break;

                        default:
                            break;
                    }

                    await this.SaveOrderAsync(order);

                    await responseStream.WriteAsync(order);

                    // Pause before the next stream write.
                    await Task.Delay(5000);
                    i++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrderStatus");
            }
        }
    }
}
