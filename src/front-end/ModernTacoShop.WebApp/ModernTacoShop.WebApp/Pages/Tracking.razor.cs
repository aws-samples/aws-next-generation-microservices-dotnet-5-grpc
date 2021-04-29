using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using ModernTacoShop.WebApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTacoShop.WebApp.Pages
{
    public partial class Tracking : IAsyncDisposable
    {
        IJSObjectReference mapModule;
        IJSObjectReference mapInstance;

        [Inject]
        Blazored.LocalStorage.ILocalStorageService LocalStorage { get; set; }

        [Inject]
        IJSRuntime JS { get; set; }

        [Inject]
        IConfiguration Configuration { get; set; }

        [Parameter]
        public string OrderId { get; set; }

        private CartModel cartModel;

        protected override async Task OnInitializedAsync()
        {
            if (await LocalStorage.ContainKeyAsync(OrderId))
            {
                cartModel = await LocalStorage.GetItemAsync<CartModel>(OrderId);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && (await LocalStorage.ContainKeyAsync(OrderId)))
            {
                var hereMapToken = "G4jajYF2Z8KiRaN-hXfWnJAU5ev6biZWBzbE2pwCnA4";
                mapModule = await JS.InvokeAsync<IJSObjectReference>("import", "./scripts/TrackingMapModule.js");
                mapInstance = await mapModule.InvokeAsync<IJSObjectReference>("initMap", hereMapToken);

                await Task.Delay(TimeSpan.FromSeconds(3));
                await StartTrackingAsync();
            }
        }


        async Task StartTrackingAsync()
        {
            var trackOrderUri = Configuration.GetValue<string>("ServiceUris:TrackOrder");

            var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
            var trackOrderChannel = GrpcChannel.ForAddress(trackOrderUri, new GrpcChannelOptions { HttpHandler = httpHandler });
            var trackOrderClient = new ModernTacoShop.TrackOrder.Protos.TrackOrder.TrackOrderClient(trackOrderChannel);
            //var tr = await cc.StartTrackingOrderAsync(new ModernTacoShop.TrackOrder.Protos.Order { OrderId = Convert.ToInt64(OrderId) });

            var statusCall = trackOrderClient.GetOrderStatus(new ModernTacoShop.TrackOrder.Protos.OrderId
            {
                Id = Convert.ToInt64(OrderId)
            });

            await foreach (var item in statusCall.ResponseStream.ReadAllAsync())
            {
                //Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(item));
                //Console.WriteLine($"lat:{item.LastUpdatedPosition.Point.Latitude} , long:{item.LastUpdatedPosition.Point.Longitude}");

                //SET Here Map
                await mapModule.InvokeVoidAsync("setPosition",
                        mapInstance,
                        item?.LastUpdatedPosition?.Point?.Latitude,
                        item?.LastUpdatedPosition?.Point?.Longitude);

                if (item.OrderStatus == TrackOrder.Protos.OrderStatus.Delivered)
                {
                    await LocalStorage.RemoveItemAsync(OrderId);
                    await LocalStorage.RemoveItemAsync("ORDER_IN_PROGRESS");
                }
            }
        }



        public async ValueTask DisposeAsync()
        {
            await mapInstance.DisposeAsync();
            await mapModule.DisposeAsync();
        }
    }
}
