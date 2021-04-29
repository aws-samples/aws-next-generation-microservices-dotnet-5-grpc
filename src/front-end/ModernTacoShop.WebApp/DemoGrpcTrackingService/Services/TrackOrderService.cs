using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ModernTacoShop.TrackOrder.Protos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemoGrpcTrackingService.Services
{
    public class TrackOrderService : TrackOrder.TrackOrderBase
    {
        private readonly ILogger<TrackOrderService> _logger;
        public TrackOrderService(ILogger<TrackOrderService> logger)
        {
            _logger = logger;
        }

        
        public override async Task GetOrderStatus(OrderId request, IServerStreamWriter<Order> responseStream, ServerCallContext context)
        {
            var trajectory = new double[,]  {
                                                {50.649410, 8.987130},
                                                {50.593649, 8.833291},
                                                {50.652893, 9.053017},
                                                {50.750309, 9.327676},
                                                {50.965296, 9.931924 },
                                                {51.003335, 10.250527},
                                                {51.006792, 10.459267},
                                                {50.899518, 10.887734},
                                                {50.909910, 11.052529},
                                                {50.889124, 11.557900},
                                                {51.020616, 11.865517},
                                                {51.237797, 12.041299},
                                                {51.330562, 12.151162},
                                                {51.703155, 12.206093},
                                                {51.774589, 12.272011},
                                                {51.998361, 12.568642},
                                                {52.173883, 12.799355},
                                                {52.187356, 12.887246 },
                                                { 52.5309916298853, 13.3846220493377 }
                                            };
            var counter = 0;
            var placedDate = DateTimeOffset.Now;
            do
            {
                var status = (trajectory.Length / 2) == (counter + 1) ? OrderStatus.Delivered : OrderStatus.InTransit;

                await responseStream.WriteAsync(new Order
                {
                    OrderId = request.Id,
                    LastUpdated = Timestamp.FromDateTimeOffset(DateTimeOffset.Now),
                    OrderPlaced = Timestamp.FromDateTimeOffset(placedDate),
                    OrderJson = "{test:123}",
                    OrderStatus = status,
                    LastUpdatedPosition = new NullablePoint
                    {
                        Point = new Point
                        {
                            Latitude = trajectory[counter, 0].ToString(),
                            Longitude = trajectory[counter, 1].ToString(),
                        }
                    }
                });
                counter++;

                await Task.Delay(TimeSpan.FromSeconds(1));

            } while ((trajectory.Length / 2) > counter);

        }
    }
}
