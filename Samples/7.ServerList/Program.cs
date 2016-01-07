﻿using System;
using System.IO;
using System.Net;
using SteamKit2;
using SteamKit2.Internal;

//
// Sample 7: ServerList
//
// this sample will give an example of how the server list can be used to
// optimize your chance of a successful connection.

namespace Sample7_ServerList
{
    class Program
    {
        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamUser steamUser;

        static bool isRunning;

        static string user, pass;


        static void Main( string[] args )
        {
            if ( args.Length < 2 )
            {
                Console.WriteLine( "Sample7: No username and password specified!" );
                return;
            }

            // save our logon details
            user = args[ 0 ];
            pass = args[ 1 ];

            // create our steamclient instance
            steamClient = new SteamClient();
            // create the callback manager which will route callbacks to function calls
            manager = new CallbackManager( steamClient );

            // get the steamuser handler, which is used for logging on after successfully connecting
            steamUser = steamClient.GetHandler<SteamUser>();

            // register a few callbacks we're interested in
            // these are registered upon creation to a callback manager, which will then route the callbacks
            // to the functions specified
            manager.Subscribe<SteamClient.ConnectedCallback>( OnConnected );
            manager.Subscribe<SteamClient.DisconnectedCallback>( OnDisconnected );

            manager.Subscribe<SteamUser.LoggedOnCallback>( OnLoggedOn );
            manager.Subscribe<SteamUser.LoggedOffCallback>( OnLoggedOff );

            Console.CancelKeyPress += ( s, e ) =>
            {
                e.Cancel = true;

                Console.WriteLine( "Received {0}, disconnecting...", e.SpecialKey );
                steamUser.LogOff();
            };

            var cellid = 0u;

            // if we've previously connected and saved our cellid, load it.
            if ( File.Exists( "cellid.txt" ) )
            {
                if ( !uint.TryParse( File.ReadAllText( "cellid.txt"), out cellid ) )
                {
                    Console.WriteLine( "Error parsing cellid from cellid.txt. Continuing with cellid 0." );
                }
                else
                {
                    Console.WriteLine( $"Using persisted cell ID {cellid}" );
                }
            }

            if ( File.Exists( "servers.bin" ) )
            {
                // last time we connected to Steam, we got a list of servers. that list is persisted below.
                // load that list of servers into the server list.
                // this is a very simplistic serialization, you're free to serialize the server list however
                // you like (json, xml, whatever).

                using ( var fs = File.OpenRead( "servers.bin" ) )
                using ( var reader = new BinaryReader( fs ) )
                {
                    while ( fs.Position < fs.Length )
                    {
                        var numAddressBytes = reader.ReadInt32();
                        var addressBytes = reader.ReadBytes( numAddressBytes );
                        var port = reader.ReadInt32();

                        var ipaddress = new IPAddress( addressBytes );
                        var endPoint = new IPEndPoint( ipaddress, port );

                        CMClient.Servers.TryAdd( endPoint );
                    }
                }

                Console.WriteLine($"Loaded {CMClient.Servers.GetAllEndPoints().Length} servers from server list cache.");
            }
            else
            {
                // since we don't have a list of servers saved, load the latest list of Steam servers
                // from the Steam Directory.
                var loadServersTask = SteamDirectory.Initialize( cellid );
                loadServersTask.Wait();

                if ( loadServersTask.IsFaulted )
                {
                    Console.WriteLine( "Error loading server list from directory: {0}", loadServersTask.Exception.Message );
                    return;
                }
            }

            isRunning = true;

            Console.WriteLine( "Connecting to Steam..." );

            // initiate the connection
            steamClient.Connect();

            // create our callback handling loop
            while ( isRunning )
            {
                // in order for the callbacks to get routed, they need to be handled by the manager
                manager.RunWaitCallbacks( TimeSpan.FromSeconds( 1 ) );
            }

            // before we exit, save our current server list to disk.
            // this is a very simplistic serialization, you're free to serialize the server list however
            // you like (json, xml, whatever).
            using ( var fs = File.OpenWrite( "servers.bin" ) )
            using ( var writer = new BinaryWriter( fs ) )
            {
                foreach ( var endPoint in CMClient.Servers.GetAllEndPoints() )
                {
                    var addressBytes = endPoint.Address.GetAddressBytes();
                    writer.Write( addressBytes.Length );
                    writer.Write( addressBytes );
                    writer.Write( endPoint.Port );
                }
            }
        }

        static void OnConnected( SteamClient.ConnectedCallback callback )
        {
            if ( callback.Result != EResult.OK )
            {
                Console.WriteLine( "Unable to connect to Steam: {0}", callback.Result );

                isRunning = false;
                return;
            }

            Console.WriteLine( "Connected to Steam! Logging in '{0}'...", user );

            steamUser.LogOn( new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,
            } );
        }

        static void OnDisconnected( SteamClient.DisconnectedCallback callback )
        {
            Console.WriteLine( "Disconnected from Steam" );

            isRunning = false;
        }

        static void OnLoggedOn( SteamUser.LoggedOnCallback callback )
        {
            if ( callback.Result != EResult.OK )
            {
                if ( callback.Result == EResult.AccountLogonDenied )
                {
                    // if we recieve AccountLogonDenied or one of it's flavors (AccountLogonDeniedNoMailSent, etc)
                    // then the account we're logging into is SteamGuard protected
                    // see sample 5 for how SteamGuard can be handled

                    Console.WriteLine( "Unable to logon to Steam: This account is SteamGuard protected." );

                    isRunning = false;
                    return;
                }

                Console.WriteLine( "Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult );

                isRunning = false;
                return;
            }

            // save the current cellid somewhere. if we lose our saved server list, we can use this when retrieving
            // servers from the Steam Directory.
            File.WriteAllText( "cellid.txt", callback.CellID.ToString() );

            Console.WriteLine( "Successfully logged on! Press Ctrl+C to log off..." );
        }

        static void OnLoggedOff( SteamUser.LoggedOffCallback callback )
        {
            Console.WriteLine( "Logged off of Steam: {0}", callback.Result );
        }
    }
}
