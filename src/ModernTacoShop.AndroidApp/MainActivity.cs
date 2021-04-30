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
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.UI.Android;
using Mapsui.Utilities;

namespace ModernTacoShop.AndroidApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        /// <summary>
        /// The user fills out this order and submits it to the backend.
        /// </summary>
        public TacoOrder order { get; set; }

        /// <summary>
        /// This map layer shows a dot that tracks the position of the order while it is in transit.
        /// </summary>
        private WritableLayer orderPositionLayer { get; set; }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            // Initialize an order.
            order = new TacoOrder();

            // Hook up UI actions.

            var submitOrderButton = FindViewById<Button>(Resource.Id.submitOrderButton);
            submitOrderButton.Click += SubmitOrderButton_Click;

            var addBeefTacoButton = FindViewById<Button>(Resource.Id.addBeefTacoButton);
            var addCarnitasTacoButton = FindViewById<Button>(Resource.Id.addCarnitasTacoButton);
            var addChickenTacoButton = FindViewById<Button>(Resource.Id.addChickenTacoButton);
            var addShrimpTacoButton = FindViewById<Button>(Resource.Id.addShrimpTacoButton);
            var addTofuTacoButton = FindViewById<Button>(Resource.Id.addTofuTacoButton);

            addBeefTacoButton.Click += ChangeOrderQuantity;
            addCarnitasTacoButton.Click += ChangeOrderQuantity;
            addChickenTacoButton.Click += ChangeOrderQuantity;
            addShrimpTacoButton.Click += ChangeOrderQuantity;
            addTofuTacoButton.Click += ChangeOrderQuantity;

            var subtractBeefTacoButton = FindViewById<Button>(Resource.Id.subtractBeefTacoButton);
            var subtractCarnitasTacoButton = FindViewById<Button>(Resource.Id.subtractCarnitasTacoButton);
            var subtractChickenTacoButton = FindViewById<Button>(Resource.Id.subtractChickenTacoButton);
            var subtractShrimpTacoButton = FindViewById<Button>(Resource.Id.subtractShrimpTacoButton);
            var subtractTofuTacoButton = FindViewById<Button>(Resource.Id.subtractTofuTacoButton);

            subtractBeefTacoButton.Click += ChangeOrderQuantity;
            subtractCarnitasTacoButton.Click += ChangeOrderQuantity;
            subtractChickenTacoButton.Click += ChangeOrderQuantity;
            subtractShrimpTacoButton.Click += ChangeOrderQuantity;
            subtractTofuTacoButton.Click += ChangeOrderQuantity;

            var resetButton = FindViewById<FloatingActionButton>(Resource.Id.resetButton);
            resetButton.Click += ResetButton_Click;

            // Initialize the map for order tracking.
            var mapControl = FindViewById<MapControl>(Resource.Id.mapControl);
            var map = new Map();
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            this.orderPositionLayer = new WritableLayer() { Style = null };
            map.Layers.Add(orderPositionLayer);
            mapControl.Map = map;
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            // Start a new order, and reset the UI.

            order = new TacoOrder();

            var orderFormLayout = FindViewById<ViewGroup>(Resource.Id.orderFormLayout);
            var orderStatusLayout = FindViewById<ViewGroup>(Resource.Id.orderStatusLayout);

            orderFormLayout.Visibility = ViewStates.Visible;
            orderStatusLayout.Visibility = ViewStates.Gone;

            var mapControl = FindViewById<MapControl>(Resource.Id.mapControl);
            mapControl.Visibility = ViewStates.Invisible;

            var resetButton = FindViewById<FloatingActionButton>(Resource.Id.resetButton);
            resetButton.Visibility = ViewStates.Gone;

            UpdateOrderFormUI();
        }

        /// <summary>
        /// Update the quantities in the order based on the UI buttons.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeOrderQuantity(object sender, EventArgs e)
        {
            var view = (View)sender;
            switch (view.Id)
            {
                case Resource.Id.addBeefTacoButton:
                    order.BeefTacoCount++;
                    break;
                case Resource.Id.addCarnitasTacoButton:
                    order.CarnitasTacoCount++;
                    break;
                case Resource.Id.addChickenTacoButton:
                    order.ChickenTacoCount++;
                    break;
                case Resource.Id.addShrimpTacoButton:
                    order.ShrimpTacoCount++;
                    break;
                case Resource.Id.addTofuTacoButton:
                    order.TofuTacoCount++;
                    break;

                case Resource.Id.subtractBeefTacoButton:
                    if (order.BeefTacoCount > 0)
                        order.BeefTacoCount--;
                    break;
                case Resource.Id.subtractCarnitasTacoButton:
                    if (order.CarnitasTacoCount > 0)
                        order.CarnitasTacoCount--;
                    break;
                case Resource.Id.subtractChickenTacoButton:
                    if (order.ChickenTacoCount > 0)
                        order.ChickenTacoCount--;
                    break;
                case Resource.Id.subtractShrimpTacoButton:
                    if (order.ShrimpTacoCount > 0)
                        order.ShrimpTacoCount--;
                    break;
                case Resource.Id.subtractTofuTacoButton:
                    if (order.TofuTacoCount > 0)
                        order.TofuTacoCount--;
                    break;

                default:
                    break;
            }

            UpdateOrderFormUI();
        }

        private void UpdateOrderFormUI()
        {
            // Update the state of the UI to match the order.

            FindViewById<Button>(Resource.Id.subtractBeefTacoButton).Enabled = order.BeefTacoCount > 0;
            FindViewById<Button>(Resource.Id.subtractCarnitasTacoButton).Enabled = order.CarnitasTacoCount > 0;
            FindViewById<Button>(Resource.Id.subtractChickenTacoButton).Enabled = order.ChickenTacoCount > 0;
            FindViewById<Button>(Resource.Id.subtractShrimpTacoButton).Enabled = order.ShrimpTacoCount > 0;
            FindViewById<Button>(Resource.Id.subtractTofuTacoButton).Enabled = order.TofuTacoCount > 0;

            FindViewById<TextView>(Resource.Id.beefTacoCountLabel).Text = order.BeefTacoCount == 0 ? "" : order.BeefTacoCount.ToString();
            FindViewById<TextView>(Resource.Id.carnitasTacoCountLabel).Text = order.CarnitasTacoCount == 0 ? "" : order.CarnitasTacoCount.ToString();
            FindViewById<TextView>(Resource.Id.chickenTacoCountLabel).Text = order.ChickenTacoCount == 0 ? "" : order.ChickenTacoCount.ToString();
            FindViewById<TextView>(Resource.Id.shrimpTacoCountLabel).Text = order.ShrimpTacoCount == 0 ? "" : order.ShrimpTacoCount.ToString();
            FindViewById<TextView>(Resource.Id.tofuTacoCountLabel).Text = order.TofuTacoCount == 0 ? "" : order.TofuTacoCount.ToString();

            FindViewById<Button>(Resource.Id.submitOrderButton).Enabled = order.TotalTacoCount() > 0;
        }

        private async void SubmitOrderButton_Click(object sender, EventArgs e)
        {
            // Submit the order, then start tracking the order status.
            // When tracking the order status, show the order status layout.

            await order.SubmitOrder();

            var orderFormLayout = FindViewById<ViewGroup>(Resource.Id.orderFormLayout);
            var orderStatusLayout = FindViewById<ViewGroup>(Resource.Id.orderStatusLayout);

            orderFormLayout.Visibility = ViewStates.Gone;
            orderStatusLayout.Visibility = ViewStates.Visible;

            await this.TrackOrderStatus(order.OrderId);
        }

        private async Task TrackOrderStatus(long orderId)
        {
            // Set up a 'dot' feature to indicate the order's location on the map.
            var orderLocationFeature = new Feature()
            {
                Styles = new List<IStyle> {
                    new SymbolStyle { Fill = new Brush(Mapsui.Styles.Color.Blue), SymbolScale = 0.5f },
                    new SymbolStyle { Fill = new Brush(Mapsui.Styles.Color.White), SymbolScale = 0.3f }
                }
            };
            orderPositionLayer.Clear();
            orderPositionLayer.Add(orderLocationFeature);

            // Stream the order status updates.
            await order.StreamOrderStatus(DisplayCurrentOrderStatus);
        }

        /// <summary>
        /// Callback that displays the current status of the order.
        /// </summary>
        /// <param name="currentStatus"></param>
        public void DisplayCurrentOrderStatus(TrackOrder.Protos.Order currentStatus)
        {
            // Show the "reset" button if the order has completed.
            if (currentStatus.OrderStatus == TrackOrder.Protos.OrderStatus.Delivered
                || currentStatus.OrderStatus == TrackOrder.Protos.OrderStatus.DeliveryError
                || currentStatus.OrderStatus == TrackOrder.Protos.OrderStatus.Cancelled)
            {
                var resetButton = FindViewById<FloatingActionButton>(Resource.Id.resetButton);
                resetButton.Visibility = ViewStates.Visible;
            }

            var statusTextView = FindViewById<TextView>(Resource.Id.orderStatusTextView);
            statusTextView.Text = $"Status: {currentStatus.OrderStatus}";

            // If there's no location data, don't try to update the map.
            if (currentStatus.LastUpdatedPosition?.Point == null)
                return;

            // Show the order location on the map.
            var mapControl = FindViewById<MapControl>(Resource.Id.mapControl);
            var map = mapControl.Map;
            mapControl.Visibility = ViewStates.Visible;

            var orderPosition = SphericalMercator.FromLonLat(
                double.Parse(currentStatus.LastUpdatedPosition.Point.Longitude),
                double.Parse(currentStatus.LastUpdatedPosition.Point.Latitude));

            orderPositionLayer.GetFeatures().First().Geometry = orderPosition;

            // Center the map on the order's current position.
            mapControl.Navigator.NavigateTo(orderPosition, map.Resolutions[15]);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
