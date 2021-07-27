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
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ModernTacoShop.TrackOrder.Protos;

namespace ModernTacoShop.TrackOrder.Server
{
    public class TrackOrderService : Protos.TrackOrder.TrackOrderBase
    {
        private readonly ILogger<TrackOrderService> _logger;
        private string _tableName;

        // Constants for the latitude & longitude coordinates of the restaurant (where orders start their transit).
        private const decimal _restaurantLatitude = 47.623211m;
        private const decimal _restaurantLongitude = -122.337158m;

        public TrackOrderService(ILogger<TrackOrderService> logger)
        {
            _logger = logger;
        }

        public override Task<Empty> HealthCheck(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> StartTrackingOrder(Order order, ServerCallContext serverCallContext)
        {
            try
            {
                if (string.IsNullOrEmpty(_tableName))
                {
                    var systemsManagementClient = new AmazonSimpleSystemsManagementClient();
                    var tableNameParameter = await systemsManagementClient.GetParameterAsync(
                        new GetParameterRequest { Name = "/ModernTacoShop/TrackOrder/OrderTableName" });
                    _tableName = tableNameParameter.Parameter.Value;
                }

                var client = new AmazonDynamoDBClient();
                var context = new DynamoDBContext(client);
                var record = new TrackOrderDynamoDbRecord(order);
                await context.SaveAsync(record, new DynamoDBOperationConfig() { OverrideTableName = _tableName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartTrackingOrder");
            }
            return new Empty();
        }

        public override async Task GetOrderStatus(OrderId request, IServerStreamWriter<Order> responseStream, ServerCallContext serverCallContext)
        {
            try
            {
                if (string.IsNullOrEmpty(_tableName))
                {
                    var systemsManagementClient = new AmazonSimpleSystemsManagementClient();
                    var tableNameParameter = await systemsManagementClient.GetParameterAsync(
                        new GetParameterRequest { Name = "/ModernTacoShop/TrackOrder/OrderTableName" });
                    _tableName = tableNameParameter.Parameter.Value;
                }

                var client = new AmazonDynamoDBClient();
                var context = new DynamoDBContext(client);
                var orderDynamoDbRecord = await context.LoadAsync<TrackOrderDynamoDbRecord>(request.Id,
                    new DynamoDBOperationConfig() { OverrideTableName = _tableName });

                var order = orderDynamoDbRecord.ToOrder();

                // Simulate an order being tracked. Update the position every few seconds.

                var maximum = 30; // 60 seconds * 5 minutes
                var i = 0;
                var random = new Random();

                while (!serverCallContext.CancellationToken.IsCancellationRequested && i <= maximum)
                {
                    switch (i)
                    {
                        case var _ when i < 5:
                            order.Status = OrderStatus.Preparing;
                            break;

                        case var _ when i >= 5 && i < maximum:
                            order.Status = OrderStatus.InTransit;

                            if (order.LastPosition == null)
                            {
                                // We don't have a position. Initialize it with the coordinates of the restaurant.
                                order.LastPosition = new Point()
                                {
                                    Latitude = _restaurantLatitude.ToString(),
                                    Longitude = _restaurantLongitude.ToString()
                                };
                            }
                            else
                            {
                                // Simulate updates to the position with realistic-ish (but fake) motion.

                                var latitudeValue = Decimal.Parse(order.LastPosition.Latitude);
                                var longitudeValue = Decimal.Parse(order.LastPosition.Longitude);

                                var latitudeIncrement = new Decimal(random.Next(5, 20)) / 100000;
                                var longitudeIncrement = -1 * new Decimal(random.Next(5, 20)) / 100000;

                                order.LastPosition.Latitude = (latitudeValue + latitudeIncrement).ToString();
                                order.LastPosition.Longitude = (longitudeValue + longitudeIncrement).ToString();
                            }
                            break;

                        case var _ when i == maximum:
                            // The order has been delivered.
                            order.Status = OrderStatus.Delivered;
                            break;

                        default:
                            break;
                    }

                    var record = new TrackOrderDynamoDbRecord(order);
                    await context.SaveAsync(record, new DynamoDBOperationConfig() { OverrideTableName = _tableName });
                    await responseStream.WriteAsync(order);

                    // Pause before the next stream write.
                    await Task.Delay(5000);
                    i++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrderStatus");
                throw;
            }
        }
    }
}
