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
using ModernTacoShop.SubmitOrder.Protos;
using ModernTacoShop.TrackOrder.Protos;

namespace AndroidApp.gRPC
{
    public class TacoOrder
    {
        // Execute this delegate as a callback when the order status stream gets new data.
        public delegate void OnOrderStatusChanged(ModernTacoShop.TrackOrder.Protos.Order orderStatus);

        public TacoOrder() { }

        public long OrderId { get; private set; }

        public uint TacoCountBeef { get; set; }

        public uint TacoCountCarnitas { get; set; }

        public uint TacoCountChicken { get; set; }

        public uint TacoCountShrimp { get; set; }

        public uint TacoCountTofu { get; set; }

        public string ServiceDomainName { get; set; }

        public uint TotalTacoCount()
        {
            return TacoCountBeef + TacoCountCarnitas + TacoCountChicken + TacoCountShrimp + TacoCountTofu;
        }

        /// <summary>
        /// Submit this order to the gRPC service. Sets the Order ID.
        /// </summary>
        public async Task SubmitOrder()
        {
            var channel = new Channel("submit-order." + ServiceDomainName, new SslCredentials());
            var client = new SubmitOrder.SubmitOrderClient(channel);

            OrderId = DateTime.UtcNow.Ticks;

            _ = await client.SubmitOrderAsync(new ModernTacoShop.SubmitOrder.Protos.Order
            {
                OrderId = OrderId,
                TacoCountBeef = TacoCountBeef,
                TacoCountCarnitas = TacoCountCarnitas,
                TacoCountChicken = TacoCountChicken,
                TacoCountShrimp = TacoCountShrimp,
                TacoCountTofu = TacoCountTofu,
                PlacedOn = Timestamp.FromDateTime(DateTime.UtcNow)
            });
        }

        /// <summary>
        /// Track the order status as it is streamed back from the gRPC service.
        /// Execute the supplied callback whenever new data come in, passing in the latest data.
        /// </summary>
        /// <param name="callback">Execute this callback whenever new order status data come in from the stream.</param>
        public async Task StreamOrderStatus(OnOrderStatusChanged callback)
        {
            var channel = new Channel("track-order." + ServiceDomainName, new SslCredentials());
            var client = new TrackOrder.TrackOrderClient(channel);

            using (var call = client.GetOrderStatus(new OrderId { Id = OrderId }))
            {
                while (await call.ResponseStream.MoveNext())
                {
                    var currentStatus = call.ResponseStream.Current;
                    callback(currentStatus);
                }
            }
        }

        public bool CanSubmit() =>
            // The order can be submitted if there is at least 1 taco, and the service domain name is set.
            !string.IsNullOrEmpty(ServiceDomainName) && TotalTacoCount() > 0;
    }
}
