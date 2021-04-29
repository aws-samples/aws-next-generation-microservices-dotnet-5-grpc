import "https://js.api.here.com/v3/3.1/mapsjs-core.js"
import "https://js.api.here.com/v3/3.1/mapsjs-service.js"

let marker = {};
export function initMap(mapToken) {
    var platform = new H.service.Platform({
        'apikey': mapToken
    });
    console.log("my map token", mapToken);

    // Get the default map types from the platform object:
    var defaultLayers = platform.createDefaultLayers();

    // Instantiate the map:
    var map = new H.Map(
        document.getElementById('topContainer'),
        defaultLayers.vector.normal.map,
        {
            zoom: 10
        });

    // Create the parameters for the routing request:
    var routingParameters = {
        'routingMode': 'fast',
        'transportMode': 'car',
        // The start point of the route:
        'origin': '50.1120423728813,8.68340740740811',
        // The end point of the route:
        'destination': '52.5309916298853,13.3846220493377',
        // Include the route shape in the response
        'return': 'polyline'
    };

    // Define a callback function to process the routing response:
    var onResult = function (result) {
        // ensure that at least one route was found
        if (result.routes.length) {
            result.routes[0].sections.forEach((section) => {
                // Create a linestring to use as a point source for the route line
                let linestring = H.geo.LineString.fromFlexiblePolyline(section.polyline);

                // Create a polyline to display the route:
                let routeLine = new H.map.Polyline(linestring, {
                    style: { strokeColor: 'blue', lineWidth: 3 }
                });

                // Create a marker for the start point:
                let startMarker = new H.map.Marker(section.departure.place.location);

                // Create a marker for the end point:
                let endMarker = new H.map.Marker(section.arrival.place.location);

                // Add the route polyline and the two markers to the map:
                map.addObjects([routeLine, startMarker, endMarker]);

                // Set the map's viewport to make the whole route visible:
                map.getViewModel().setLookAtData({ bounds: routeLine.getBoundingBox() });
            });
        }
    };

    // Get an instance of the routing service version 8:
    var router = platform.getRoutingService(null, 8);

    // Call calculateRoute() with the routing parameters,
    // the callback and an error callback function (called if a
    // communication error occurs):
    router.calculateRoute(routingParameters, onResult,
        function (error) {
            alert(error.message);
        });

    // Create an icon object, an object with geographic coordinates and a marker:
    var icon = new H.map.Icon('/imgs/car.png'); // new H.map.DomIcon(animatedSvg);
    // Get an instance of the geocoding service:
    var service = platform.getSearchService();


    // Call the geocode method with the geocoding parameters,
    // the callback and an error callback function (called if a
    // communication error occurs):
    service.reverseGeocode({
        at: '50.1120423728813,8.68340740740811'
    }, (result) => {
        // Add a marker for each location found
        result.items.forEach((item) => {
            marker = new H.map.Marker(item.position, { icon: icon });
            map.addObject(marker);
        });
    }, alert);

    return map;
}

export function setPosition(mapInstance, latitude, longitude) {
    //console.log(mapInstance);
    //console.log("lat:", latitude);
    //console.log("long:", longitude);

    if (isEmpty(mapInstance) || isEmpty(latitude) || isEmpty(longitude)) {
        console.error("Empty param");
        return;
    }

    let position = { lat: latitude, lng: longitude };

    ease(
        marker.getGeometry(),
        position,
        200,
        function (coord) {
            marker.setGeometry(coord);
        }
    )

    // center map on venue
    mapInstance.setCenter(position);
}

function isEmpty(obj) {
    return (obj === null || obj === undefined || Object.keys(obj).length === 0 && obj.constructor === Object)
}

/**
   * Ease function
   * @param   {H.geo.IPoint} startCoord   start geo coordinate
   * @param   {H.geo.IPoint} endCoord     end geo coordinate
   * @param   number durationMs           duration of animation between start & end coordinates
   * @param   function onStep             callback executed each step
   * @param   function onStep             callback executed at the end
   */
function ease(
    startCoord = { lat: 0, lng: 0 },
    endCoord = { lat: 1, lng: 1 },
    durationMs = 200,
    onStep = console.log,
    onComplete = function () { },
) {
    var raf = window.requestAnimationFrame || function (f) { window.setTimeout(f, 16) },
        stepCount = durationMs / 16,
        valueIncrementLat = (endCoord.lat - startCoord.lat) / stepCount,
        valueIncrementLng = (endCoord.lng - startCoord.lng) / stepCount,
        sinValueIncrement = Math.PI / stepCount,
        currentValueLat = startCoord.lat,
        currentValueLng = startCoord.lng,
        currentSinValue = 0;

    function step() {
        currentSinValue += sinValueIncrement;
        currentValueLat += valueIncrementLat * (Math.sin(currentSinValue) ** 2) * 2;
        currentValueLng += valueIncrementLng * (Math.sin(currentSinValue) ** 2) * 2;

        if (currentSinValue < Math.PI) {
            onStep({ lat: currentValueLat, lng: currentValueLng });
            raf(step);
        } else {
            onStep(endCoord);
            onComplete();
        }
    }

    raf(step);
}

