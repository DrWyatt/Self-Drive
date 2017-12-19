using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Rage;
using Rage.Attributes;
using Rage.Native;
using RAGENativeUI;
using RAGENativeUI.Elements;

[assembly: Plugin("Self-Drive", Description = "Self-Drive Plugin", Author = "AvChaotic101")]

namespace SelfDrive
{
    public class EntryPoint
    {
#if DEBUG
        private static UIMenu mainMenu;
        private static UIMenu routeSelector;
        private static MenuPool _menuPool;
        private static UIMenuItem navigateToRouteSelector;
        private static UIMenuListItem routeList;
        private static UIMenuItem confirmRoute;
        private static UIMenuItem croute;
        private static UIMenuItem droute;

        static string[] filePaths = Directory.GetFiles(Directory.GetCurrentDirectory() + @"\Plugins\SelfDrive\Routes", "*.xml", SearchOption.AllDirectories);

#endif

        private static readonly INIFile Config = new INIFile(Directory.GetCurrentDirectory() + @"\Plugins\SelfDrive.ini");
#if DEBUG

        private static List<dynamic> listWithRoutes = new List<dynamic>(){};
        private static List<dynamic> emptyList = new List<dynamic>() {"Empty" };

        private static bool isinRoute = false;
        private static string loadedRoute;
        private static bool isRouting;
        private static IEnumerable<Waypoint> waypoints;
#endif

        public static void Main()
        {
            Game.DisplayNotification("~b~Self-Drive v1.1 by ~y~AvChaotic101 ~b~ loaded!");
#if DEBUG
            _menuPool = new MenuPool();
            mainMenu = new UIMenu("Self-Drive", "");
            _menuPool.Add(mainMenu);
            routeSelector = new UIMenu("Route Manager", "Manage Your Routes");
            _menuPool.Add(routeSelector);
            mainMenu.AddItem(navigateToRouteSelector = new UIMenuItem("Route Manager Menu"));
            mainMenu.BindMenuToItem(routeSelector, navigateToRouteSelector);
            routeSelector.ParentMenu = mainMenu;
            mainMenu.RefreshIndex();
            routeSelector.RefreshIndex();
            mainMenu.OnItemSelect += OnItemSelect;
            Game.FrameRender += RenderingText;
            foreach(string file in filePaths)
            {
                listWithRoutes.Add(file);
            }
            if(filePaths.Count() > 0)
            {
                routeList = new UIMenuListItem("Routes", listWithRoutes, 0);
                routeSelector.AddItem(routeList);
            } else
            {
                routeList = new UIMenuListItem("Routes", emptyList, 0);
                routeSelector.AddItem(routeList);
            }
            mainMenu.AddItem(confirmRoute = new UIMenuItem("Start Route"));
            mainMenu.AllowCameraMovement = true;
            mainMenu.MouseControlsEnabled = true;
            routeSelector.AllowCameraMovement = true;
            routeSelector.MouseControlsEnabled = true;
            MenuMainLogic();
#endif
            MainLogic();
            //Route.main();
        }

#if DEBUG

        public static void RenderingText(object sender, GraphicsEventArgs e)
        {
            //e.Graphics.DrawText("Loaded Route: "+ loadedRoute)
            if (isinRoute)
            {
                if (isRouting)
                    e.Graphics.DrawText("Loaded Route: " + loadedRoute, "Arial", 20, new System.Drawing.PointF(0, 0), System.Drawing.Color.White);
                else
                    e.Graphics.DrawText("Loaded Route: " + loadedRoute, "Arial", 20, new System.Drawing.PointF(0, 0), System.Drawing.Color.White);
            }
            else
                e.Graphics.DrawText("No Loaded Routes", "Arial", 20, new System.Drawing.PointF(0, 0), System.Drawing.Color.White);
        }

        public static void OnItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (sender == mainMenu)
            {
                if (selectedItem == confirmRoute && !isinRoute)
                {
                    if (routeList.Items[routeList.Index] != "Empty")
                    {
                        GameFiber.StartNew(delegate
                        {
                            GameFiber.Sleep(500);
                            Game.DisplayNotification("Route Loaded");
                            isinRoute = !isinRoute;     
                            confirmRoute.Text = "Stop Route";
                            loadedRoute = routeList.Items[routeList.Index];
                        });
                    }
                }

                if(selectedItem == confirmRoute && isinRoute)
                {
                    GameFiber.StartNew(delegate
                    {
                        isinRoute = !isinRoute;
                        GameFiber.Sleep(500);
                        Game.DisplayNotification("Route Unloaded");
                        confirmRoute.Text = "Start Route";
                        loadedRoute = "Empty";
                    });
                }
            }
        }
#endif

        private static void SetAgressiveness()
        {
            NativeFunction.Natives.SET_DRIVER_AGGRESSIVENESS(Game.LocalPlayer.Character,
                float.Parse(Config.Read("DriverSettings", "DriverAgressiveness")));
        }

        public static Keys ConvertFromString(string keystr)
        {
            return (Keys) Enum.Parse(typeof (Keys), keystr);
        }

#if DEBUG
        private static void MenuMainLogic()
        {
            GameFiber.StartNew(delegate
            {
                GameFiber.Yield();
                Game.LogTrivial("StartedMainMenuLogic");
                while (true)
                {
                    GameFiber.Yield();
                    if (Game.IsKeyDown(Keys.F12))
                    {
                        mainMenu.Visible = !mainMenu.Visible;
                    }
                    _menuPool.ProcessMenus();

                }
            });
        }
#endif

        private static void MainLogic()
        {
            GameFiber.StartNew(delegate
            {
                /*string[] files = new RouteReader().GetFilesFromFolder();
                foreach (string element in files)
                {
                    Game.DisplayNotification("Loaded ~g~" + Path.GetFileNameWithoutExtension(element));
                }*/
                GameFiber.Yield();
                var isEngaged = false;
                var isWaypoint = false;
                var started = false;
                var config = new INIFile(Directory.GetCurrentDirectory() + @"\Plugins\SelfDrive.ini");
                //Game.DisplayNotification(Directory.GetCurrentDirectory() + @"\Plugins\SelfDrive.ini");
                var speed = Convert.ToInt32(config.Read("SpeedManagement", "CruiseSpeed"));
                var increaseSpeed = ConvertFromString(config.Read("Settings", "IncreaseSpeed"));
                var decreaseSpeed = ConvertFromString(config.Read("Settings", "DecreaseSpeed"));
                var startStop = ConvertFromString(config.Read("Settings", "StartStop"));
                //System.Media.SoundPlayer player = new System.Media.SoundPlayer(Directory.GetCurrentDirectory() + @"\Plugins\SelfDrive\BEEP.wav");                
                var currentWayPointPosition = new Vector3(0, 0, 0);
                //bool IsWaypointOn = World.GetWaypointBlip().IsValid();
                //Blip waypoint = World.GetAllBlips().FirstOrDefault(b => b.Sprite == BlipSprite.Waypoint);
                //Game.LogTrivial("StartedMainLogic");
                while (true)
                {
                    GameFiber.Yield();
                    if (Game.IsKeyDown(startStop) && Game.LocalPlayer.Character.IsOnFoot == false &&
                        Game.LocalPlayer.Character.IsPassenger == false && Game.LocalPlayer.Character.IsDead == false &&
                        Game.LocalPlayer.Character.IsInAirVehicle == false &&
                        Game.LocalPlayer.Character.IsInSeaVehicle == false &&
                        Game.LocalPlayer.Character.IsStunned == false)
                    {
                        switch (isEngaged)
                        {
                            case false:
                                Game.DisplayNotification("~g~Self-Driving Engaged");
                                SetAgressiveness();
#if DEBUG
                                if (!isinRoute)
                                {
#endif
                                if (World.GetWaypointBlip() != null)
                                {
                                    //player.Play();
                                    Game.LocalPlayer.Character.Tasks.DriveToPosition(World.GetWaypointBlip().Position,
                                        speed, VehicleDrivingFlags.Normal | VehicleDrivingFlags.FollowTraffic);
                                    currentWayPointPosition = World.GetWaypointBlip().Position;
                                    Game.DisplayNotification("~g~Driving to Waypoint");
                                    started = true;
                                    isWaypoint = true;
                                }
                                else
                                {
                                    //player.Play();
                                    Game.LocalPlayer.Character.Tasks.CruiseWithVehicle(speed,
                                        VehicleDrivingFlags.Normal | VehicleDrivingFlags.AvoidHighways |
                                        VehicleDrivingFlags.FollowTraffic);
                                    started = true;
                                }
#if DEBUG
                        }
#endif
#if DEBUG
                                else
                                {
                                    Game.DisplayNotification("~g~Following Route"); 
                                }
#endif
                                isEngaged = !isEngaged;
                                //GameFiber.Sleep(5000);
                                break;


                            case true:
                                //player.Play();
                                //GameFiber.Sleep(400);
                                //player.Play();
                                Game.DisplayNotification("~r~Self-Driving Disengaged");
                                Game.LocalPlayer.Character.Tasks.Clear();
                                isEngaged = !isEngaged;
                                //GameFiber.Sleep(5000);
                                break;
                        }
                    }

                    if (Game.IsKeyDown(increaseSpeed) && isEngaged)
                    {
                        speed++;
                        Game.LocalPlayer.Character.Tasks.Clear();
                        Game.DisplayHelp("Current Speed: ~g~" +
                                         Convert.ToInt32(Convert.ToInt32(Convert.ToInt32(speed)/0.44704)/2));
                        if (World.GetWaypointBlip() != null && started)
                        {
                            Game.LocalPlayer.Character.Tasks.DriveToPosition(World.GetWaypointBlip().Position, speed,
                                VehicleDrivingFlags.Normal | VehicleDrivingFlags.FollowTraffic);
                            currentWayPointPosition = World.GetWaypointBlip().Position;
                        }
                        else
                        {
                            Game.LocalPlayer.Character.Tasks.CruiseWithVehicle(speed,
                                VehicleDrivingFlags.Normal | VehicleDrivingFlags.AvoidHighways |
                                VehicleDrivingFlags.FollowTraffic);
                        }
                    }

                    if (Game.IsKeyDown(decreaseSpeed) && isEngaged)
                    {
                        speed--;
                        Game.LocalPlayer.Character.Tasks.Clear();
                        Game.DisplayHelp("Current Speed: ~g~" +
                                         Convert.ToInt32(Convert.ToInt32(Convert.ToInt32(speed)/0.44704)/2));
                        if (World.GetWaypointBlip() != null && started)
                        {
                            Game.LocalPlayer.Character.Tasks.DriveToPosition(World.GetWaypointBlip().Position, speed,
                                VehicleDrivingFlags.Normal | VehicleDrivingFlags.FollowTraffic);
                        }
                        else
                        {
                            Game.LocalPlayer.Character.Tasks.CruiseWithVehicle(speed,
                                VehicleDrivingFlags.Normal | VehicleDrivingFlags.AvoidHighways |
                                VehicleDrivingFlags.FollowTraffic);
                        }
                    }

                    if (isWaypoint && World.GetWaypointBlip() == null && started)
                    {
                        switch (config.Read("Settings", "StopAtWayPoint"))
                        {
                            case "true":
                                Game.DisplayNotification("~r~Self-Driving Disengaged");
                                Game.LocalPlayer.Character.CurrentVehicle.Velocity = new Vector3(0, 0, 0);
                                Game.LocalPlayer.Character.Tasks.Clear();
                                isEngaged = !isEngaged;
                                isWaypoint = false;
                                break;

                            case "false":
                                Game.DisplayNotification("~r~Arrived at Waypoint, Enabling Random Route");
                                Game.LocalPlayer.Character.Tasks.Clear();
                                Game.LocalPlayer.Character.Tasks.CruiseWithVehicle(speed,
                                    VehicleDrivingFlags.Normal | VehicleDrivingFlags.AvoidHighways |
                                    VehicleDrivingFlags.FollowTraffic);
                                isWaypoint = false;
                                break;
                        }
                    }

                    if (isEngaged && !isWaypoint && World.GetWaypointBlip() != null && started)
                    {
                        Game.LocalPlayer.Character.Tasks.Clear();
                        Game.LocalPlayer.Character.Tasks.DriveToPosition(World.GetWaypointBlip().Position, speed,
                            VehicleDrivingFlags.Normal | VehicleDrivingFlags.FollowTraffic);
                        currentWayPointPosition = World.GetWaypointBlip().Position;
                        Game.DisplayNotification("~g~Driving to Waypoint");
                        isWaypoint = true;
                    }

                    if (isEngaged && started && World.GetWaypointBlip() != null &&
                        currentWayPointPosition != World.GetWaypointBlip().Position)
                    {
                        Game.LocalPlayer.Character.Tasks.DriveToPosition(World.GetWaypointBlip().Position, speed,
                            VehicleDrivingFlags.Normal | VehicleDrivingFlags.FollowTraffic);
                        currentWayPointPosition = World.GetWaypointBlip().Position;
                        Game.DisplayNotification("~g~Driving to Waypoint");
                    }
                }
            });
        }
    }
}