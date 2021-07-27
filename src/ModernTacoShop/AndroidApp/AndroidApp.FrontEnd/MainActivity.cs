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
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidApp.gRPC;
using AndroidX.AppCompat.App;
using Google.Android.Material.FloatingActionButton;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.UI.Android;
using Mapsui.Utilities;

namespace AndroidApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        /// <summary>
        /// The user fills out this order and submits it to the backend.
        /// </summary>
        private TacoOrder TacoOrder { get; set; }

        /// <summary>
        /// This map layer shows a dot that tracks the position of the order while it is in transit.
        /// </summary>
        private WritableLayer OrderPositionLayer { get; set; }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // Initialize the order.
            TacoOrder = new TacoOrder();

            // Hook up the domain name text field.
            var domainNameText = FindViewById<EditText>(Resource.Id.domainNameText);
            domainNameText.TextChanged += (sender, e) =>
            {
                TacoOrder.ServiceDomainName = e.Text.ToString().Trim();
                FindViewById<Button>(Resource.Id.submitOrderButton).Enabled = TacoOrder.CanSubmit();
            };

            // Hook up UI buttons.
            var quantityButtonIds = new List<int>()
            {
                Resource.Id.addBeefTacoButton,
                Resource.Id.addCarnitasTacoButton,
                Resource.Id.addChickenTacoButton,
                Resource.Id.addShrimpTacoButton,
                Resource.Id.addTofuTacoButton,
                Resource.Id.subtractBeefTacoButton,
                Resource.Id.subtractCarnitasTacoButton,
                Resource.Id.subtractChickenTacoButton,
                Resource.Id.subtractShrimpTacoButton,
                Resource.Id.subtractTofuTacoButton
            };

            foreach (var buttonId in quantityButtonIds)
            {
                var button = FindViewById<Button>(buttonId);
                button.Click += ChangeOrderQuantity;
            }

            var resetButton = FindViewById<FloatingActionButton>(Resource.Id.resetButton);
            resetButton.Click += ResetButton_Click;

            var submitOrderButton = FindViewById<Button>(Resource.Id.submitOrderButton);
            submitOrderButton.Click += SubmitOrderButton_Click;

            // Initialize the map for order tracking.
            var mapControl = FindViewById<MapControl>(Resource.Id.mapControl);
            var map = new Map();
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            OrderPositionLayer = new WritableLayer() { Style = null };
            map.Layers.Add(OrderPositionLayer);
            mapControl.Map = map;
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
                    TacoOrder.TacoCountBeef++;
                    break;
                case Resource.Id.addCarnitasTacoButton:
                    TacoOrder.TacoCountCarnitas++;
                    break;
                case Resource.Id.addChickenTacoButton:
                    TacoOrder.TacoCountChicken++;
                    break;
                case Resource.Id.addShrimpTacoButton:
                    TacoOrder.TacoCountShrimp++;
                    break;
                case Resource.Id.addTofuTacoButton:
                    TacoOrder.TacoCountTofu++;
                    break;

                case Resource.Id.subtractBeefTacoButton:
                    if (TacoOrder.TacoCountBeef > 0)
                        TacoOrder.TacoCountBeef--;
                    break;
                case Resource.Id.subtractCarnitasTacoButton:
                    if (TacoOrder.TacoCountCarnitas > 0)
                        TacoOrder.TacoCountCarnitas--;
                    break;
                case Resource.Id.subtractChickenTacoButton:
                    if (TacoOrder.TacoCountChicken > 0)
                        TacoOrder.TacoCountChicken--;
                    break;
                case Resource.Id.subtractShrimpTacoButton:
                    if (TacoOrder.TacoCountShrimp > 0)
                        TacoOrder.TacoCountShrimp--;
                    break;
                case Resource.Id.subtractTofuTacoButton:
                    if (TacoOrder.TacoCountTofu > 0)
                        TacoOrder.TacoCountTofu--;
                    break;

                default:
                    break;
            }

            UpdateOrderFormUI();
        }

        /// <summary>
        /// Callback that displays the current status of the order.
        /// </summary>
        /// <param name="trackedOrder"></param>
        private void DisplayCurrentOrderStatus(ModernTacoShop.TrackOrder.Protos.Order trackedOrder)
        {
            // Show the "reset" button if the order has completed.
            if (trackedOrder.Status == ModernTacoShop.TrackOrder.Protos.OrderStatus.Delivered
                || trackedOrder.Status == ModernTacoShop.TrackOrder.Protos.OrderStatus.DeliveryError
                || trackedOrder.Status == ModernTacoShop.TrackOrder.Protos.OrderStatus.Cancelled)
            {
                var resetButton = FindViewById<FloatingActionButton>(Resource.Id.resetButton);
                resetButton.Visibility = ViewStates.Visible;
            }

            var statusTextView = FindViewById<TextView>(Resource.Id.orderStatusTextView);
            statusTextView.Text = $"Status: {trackedOrder.Status}";

            // If there's no location data, don't try to update the map.
            if (trackedOrder.LastPosition == null)
                return;

            // Show the order location on the map.
            var mapControl = FindViewById<MapControl>(Resource.Id.mapControl);
            var map = mapControl.Map;
            mapControl.Visibility = ViewStates.Visible;

            var orderPosition = SphericalMercator.FromLonLat(
                double.Parse(trackedOrder.LastPosition.Longitude),
                double.Parse(trackedOrder.LastPosition.Latitude));

            OrderPositionLayer.GetFeatures().First().Geometry = orderPosition;

            // Center the map on the order's current position.
            mapControl.Navigator.NavigateTo(orderPosition, map.Resolutions[15]);
        }


        /// <summary>
        /// Handle a click event on the reset button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetButton_Click(object sender, EventArgs e)
        {
            // Start a new order, and reset the UI.

            TacoOrder = new TacoOrder();

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
        /// Submit the order, then start tracking the order status.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SubmitOrderButton_Click(object sender, EventArgs e)
        {
            await TacoOrder.SubmitOrder();

            // When tracking the order status, show the order status layout.
            var orderFormLayout = FindViewById<ViewGroup>(Resource.Id.orderFormLayout);
            var orderStatusLayout = FindViewById<ViewGroup>(Resource.Id.orderStatusLayout);

            orderFormLayout.Visibility = ViewStates.Gone;
            orderStatusLayout.Visibility = ViewStates.Visible;

            // Set up a 'dot' feature to indicate the order's location on the map.
            var orderLocationFeature = new Feature()
            {
                Styles = new List<IStyle> {
                    new SymbolStyle { Fill = new Brush(Color.Blue), SymbolScale = 0.5f },
                    new SymbolStyle { Fill = new Brush(Color.White), SymbolScale = 0.3f }
                }
            };
            OrderPositionLayer.Clear();
            OrderPositionLayer.Add(orderLocationFeature);

            // Stream the order status updates.
            await TacoOrder.StreamOrderStatus(DisplayCurrentOrderStatus);
        }

        /// <summary>
        /// Update the state of the UI to match the order.
        /// </summary>
        private void UpdateOrderFormUI()
        {
            FindViewById<EditText>(Resource.Id.domainNameText).Text = TacoOrder.ServiceDomainName;

            FindViewById<Button>(Resource.Id.subtractBeefTacoButton).Enabled = TacoOrder.TacoCountBeef > 0;
            FindViewById<Button>(Resource.Id.subtractCarnitasTacoButton).Enabled = TacoOrder.TacoCountCarnitas > 0;
            FindViewById<Button>(Resource.Id.subtractChickenTacoButton).Enabled = TacoOrder.TacoCountChicken > 0;
            FindViewById<Button>(Resource.Id.subtractShrimpTacoButton).Enabled = TacoOrder.TacoCountShrimp > 0;
            FindViewById<Button>(Resource.Id.subtractTofuTacoButton).Enabled = TacoOrder.TacoCountTofu > 0;

            FindViewById<TextView>(Resource.Id.beefTacoCountLabel).Text = TacoOrder.TacoCountBeef == 0 ? "" : TacoOrder.TacoCountBeef.ToString();
            FindViewById<TextView>(Resource.Id.carnitasTacoCountLabel).Text = TacoOrder.TacoCountCarnitas == 0 ? "" : TacoOrder.TacoCountCarnitas.ToString();
            FindViewById<TextView>(Resource.Id.chickenTacoCountLabel).Text = TacoOrder.TacoCountChicken == 0 ? "" : TacoOrder.TacoCountChicken.ToString();
            FindViewById<TextView>(Resource.Id.shrimpTacoCountLabel).Text = TacoOrder.TacoCountShrimp == 0 ? "" : TacoOrder.TacoCountShrimp.ToString();
            FindViewById<TextView>(Resource.Id.tofuTacoCountLabel).Text = TacoOrder.TacoCountTofu == 0 ? "" : TacoOrder.TacoCountTofu.ToString();

            FindViewById<Button>(Resource.Id.submitOrderButton).Enabled = TacoOrder.CanSubmit();
        }
    }
}
