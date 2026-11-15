using System;
using UnityEngine;

namespace Homecoming.Field
{
    [Serializable] public sealed class WeatherProxySelection
    {
        public float requestedLatitude, requestedLongitude, siteLatitude, siteLongitude, distanceKm;
        public string siteId, siteName, provenance;

        internal bool Matches(string site) => !string.IsNullOrEmpty(siteId) && siteId == site;
    }

    public sealed partial class ScenarioRun
    {
        public WeatherProxySelection weatherProxy;
    }

    public sealed partial class FieldSimulation
    {
        Vector2? requestedWeatherPoint;
        WeatherProxySelection activeWeatherProxy, transferWeatherProxy, reportWeatherProxy;

        static float SiteDistanceKm(Vector2 point, RegionClimate site)
        {
            double latitude = point.x * Math.PI / 180;
            double siteLatitude = site.latitude * Math.PI / 180;
            double halfLongitude = (site.longitude - point.y) * Math.PI / 360;
            double sinLatitude = Math.Sin((siteLatitude - latitude) / 2);
            double sinLongitude = Math.Sin(halfLongitude);
            double haversine = sinLatitude * sinLatitude +
                Math.Cos(latitude) * Math.Cos(siteLatitude) * sinLongitude * sinLongitude;
            return (float)(12742 * Math.Asin(Math.Sqrt(Math.Clamp(haversine,0,1))));
        }

        int ClosestWeatherSite(Vector2 point)
        {
            int nearest = 0;
            for (int i = 1; i < data.regions.Length; i++)
                if (SiteDistanceKm(point,data.regions[i]) < SiteDistanceKm(point,data.regions[nearest]))
                    nearest = i;
            return nearest;
        }

        static WeatherProxySelection DescribeWeatherProxy(Vector2 point, RegionClimate site) => new()
        {
            requestedLatitude = point.x, requestedLongitude = point.y,
            siteLatitude = site.latitude, siteLongitude = site.longitude, siteId = site.id, siteName = site.name,
            distanceKm = SiteDistanceKm(point,site), provenance = "Built-in weather preset · 1991–2020 NASA POWER normals"
        };

        void AcceptWeatherProxy(Vector2 point, int index)
        {
            var proxy = DescribeWeatherProxy(point,data.regions[index]);
            if (index == siteIndex)
            {
                activeWeatherProxy = proxy;
                showSites = false;
            }
            else if (!BeginSiteTransfer(index,proxy)) return;
            requestedWeatherPoint = null;
        }

        void BindWeatherProxy(ScenarioPreview preview)
        {
            if (!IsGuidedScenario && activeWeatherProxy?.Matches(preview.Run.editedSiteId) == true)
                preview.Run.weatherProxy = activeWeatherProxy;
        }

        void BindReportWeatherProxy(CascadeResult result)
        {
            reportWeatherProxy = null;
            if (result.siteId != result.editedSiteId || string.IsNullOrEmpty(result.runId)) return;
            var run = result.provenance == CascadeProvenance.Preview && pendingPreview?.Id == result.runId
                ? pendingPreview.Run : campaign?.GetRun(result.runId);
            if (run?.weatherProxy?.Matches(result.siteId) == true) reportWeatherProxy = run.weatherProxy;
        }

        void DrawReportWeatherProxy()
        {
            if (reportWeatherProxy == null) return;
            DrawWeatherProxyText(841,49,690,reportWeatherProxy);
        }

        void DrawTransferWeatherProxy()
        {
            if (transferWeatherProxy == null) return;
            DrawWeatherProxyText(867,109,680,transferWeatherProxy);
        }

        void DrawLabWeatherProxy()
        {
            if (activeWeatherProxy?.Matches(Region.id) != true) return;
            Card(Box(54,379,852,81),Ink);
            DrawWeatherProxyText(75,379,808,activeWeatherProxy);
        }

        void DrawWeatherProxyText(float x, float y, float width, WeatherProxySelection proxy)
        {
            string requested = Coordinate(proxy.requestedLatitude,proxy.requestedLongitude);
            string site = Coordinate(proxy.siteLatitude,proxy.siteLongitude);
            Text(Box(x,y,width,26),$"Requested location   {requested}",19,White,strongFont);
            Text(Box(x,y+28,width,26),$"Simulation proxy   {proxy.siteName} · {site} · {ProxyDistance(proxy.distanceKm)} away",17,Mint);
            Text(Box(x,y+55,width,25),proxy.provenance,16,Dim);
        }

        static string ProxyDistance(float km) => km switch
        {
            0 => "0 km",
            < .1f => "<0.1 km",
            < 10 => km.ToString("0.#",Invariant) + " km",
            _ => km.ToString("N0",Invariant) + " km"
        };

        void DrawSitePicker(float top, bool fly)
        {
            if (!fly)
            {
                Card(Box(54,top-7,290,248),Ink);
                for (int i = 0; i < data.regions.Length; i++)
                    if (Button(Box(60,top+i*39,278,35),data.regions[i].name,active:siteIndex == i,size:17))
                    {
                        Select(i);
                        showSites = false;
                    }
                return;
            }

            Card(Box(54,top,1017,365),Ink);
            var map = Box(70,top+16,666,333);
            DrawEarthMap(map);
            int nearest = requestedWeatherPoint.HasValue ? ClosestWeatherSite(requestedWeatherPoint.Value) : -1;
            if (nearest >= 0)
            {
                var site = data.regions[nearest];
                DrawChartSegment(MapPosition(map,requestedWeatherPoint.Value),
                    MapPosition(map,new Vector2(site.latitude,site.longitude)),Yellow);
            }
            for (int i = 0; i < data.regions.Length; i++)
            {
                var site = data.regions[i];
                if (!DrawMapPlace(map,new Vector2(site.latitude,site.longitude),site.name,nearest < 0 ? siteIndex == i : nearest == i,false,
                    site.id == GuidedRainOrigin)) continue;
                if (siteIndex != i) BeginSiteTransfer(i);
                else
                {
                    activeWeatherProxy = null;
                    showSites = false;
                }
                requestedWeatherPoint = null;
                return;
            }

            var cursor = Event.current.mousePosition;
            if (GUI.Button(map,GUIContent.none,GUIStyle.none)) requestedWeatherPoint = MapCoordinate(map,cursor);

            const float x = 762;
            Text(Box(x,top+22,287,42),"Choose a simulation site",25,White,headingFont);
            if (requestedWeatherPoint is not Vector2 point)
            {
                if (activeWeatherProxy?.Matches(Region.id) == true)
                    DrawMapSelection(map,new Vector2(activeWeatherProxy.requestedLatitude,activeWeatherProxy.requestedLongitude));
                Text(Box(x,top+83,290,104),"Choose a built-in site, or click the map to see the closest simulation to your location.",
                    22,Dim,bodyFont,TextAnchor.UpperLeft,true);
                Tag(x,top+230,"CURRENT SIMULATION SITE",Dim);
                Text(Box(x,top+256,290,44),Region.name,23,Mint,strongFont);
                return;
            }

            DrawMapSelection(map,point);
            int closest = ClosestWeatherSite(point);
            var proxy = DescribeWeatherProxy(point,data.regions[closest]);
            Tag(x,top+80,"REQUESTED LOCATION",Dim);
            Text(Box(x,top+107,290,28),Coordinate(point.x,point.y),20,White);
            Tag(x,top+151,"CLOSEST SIMULATION SITE",Dim);
            Text(Box(x,top+178,290,29),proxy.siteName,21,Mint,strongFont);
            Text(Box(x,top+207,290,26),$"{Coordinate(proxy.siteLatitude,proxy.siteLongitude)} · {ProxyDistance(proxy.distanceKm)} away",16,White);
            Text(Box(x,top+241,290,54),proxy.provenance,17,Dim,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(x,top+308,291,42),"Use this site as a proxy",accent:true,size:19)) AcceptWeatherProxy(point,closest);
        }
    }
}
