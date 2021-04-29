﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using Grpc.Core;

namespace ModernTacoShop.AndroidApp
{
    public class TacoOrder
    {
        // Domain names for the gRPC services. Change these to your backend services.
        private const string SubmitOrderServiceDomainName = "submit-order.HOSTED_ZONE_DOMAIN_NAME";
        private const string TrackOrderServiceDomainName = "track-order.HOSTED_ZONE_DOMAIN_NAME";

        // Execute this delegate as a callback when the order status stream gets new data.
        public delegate void OnOrderStatusChanged(TrackOrder.Protos.Order orderStatus);

        public TacoOrder() { }

        public uint BeefTacoCount { get; set; }

        public uint CarnitasTacoCount { get; set; }

        public uint ChickenTacoCount { get; set; }

        public uint ShrimpTacoCount { get; set; }

        public uint TofuTacoCount { get; set; }

        public long OrderId { get; private set; }

        public uint TotalTacoCount()
        {
            return BeefTacoCount + CarnitasTacoCount + ChickenTacoCount + ShrimpTacoCount + TofuTacoCount;
        }

        /// <summary>
        /// Submit this order to the gRPC service. Sets the Order ID.
        /// </summary>
        public async Task SubmitOrder()
        {
            var orderJson = JsonSerializer.Serialize(this);

            var channel = new Channel(SubmitOrderServiceDomainName, new SslCredentials());
            var client = new ModernTacoShop.SubmitOrder.Protos.SubmitOrder.SubmitOrderClient(channel);

            this.OrderId = DateTime.UtcNow.Ticks;
            _ = await client.SubmitOrderAsync(new SubmitOrder.Protos.Order
            {
                OrderId = this.OrderId,
                OrderJson = orderJson
            });
        }

        /// <summary>
        /// Track the order status as it is streamed back from the gRPC service.
        /// Execute the supplied callback whenever new data come in, passing in the latest data.
        /// </summary>
        /// <param name="callback">Execute this callback whenever new order status data come in from the stream.</param>
        public async Task StreamOrderStatus(OnOrderStatusChanged callback)
        {
            var channel = new Channel(TrackOrderServiceDomainName, new SslCredentials());
            var client = new ModernTacoShop.TrackOrder.Protos.TrackOrder.TrackOrderClient(channel);

            using (var call = client.GetOrderStatus(new TrackOrder.Protos.OrderId() { Id = this.OrderId }))
            {
                while (await call.ResponseStream.MoveNext())
                {
                    var currentStatus = call.ResponseStream.Current;
                    callback(currentStatus);
                }
            }
        }


    }
}