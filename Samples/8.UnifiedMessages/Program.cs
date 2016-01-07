﻿using System;

using SteamKit2;
using SteamKit2.Unified.Internal;

//
// Sample 8: Unified Messages
//
// this sample introduces the usage of the unified service API
//
// unified services are a type of webapi service that can be accessed with either
// HTTP requests or through the Steam network
//
// in this case, this sample will demonstrate using the IPlayer unified service
// through the connection to steam
//

namespace Sample8_UnifiedMessages
{
    class Program
    {
        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamUser steamUser;

        static SteamUnifiedMessages steamUnifiedMessages;
        static SteamUnifiedMessages.UnifiedService<IPlayer> playerService;

        static bool isRunning;

        static string user, pass;

        static JobID badgeRequest = JobID.Invalid;


        static void Main( string[] args )
        {
            if ( args.Length < 2 )
            {
                Console.WriteLine( "Sample8: No username and password specified!" );
                return;
            }

            // save our logon details
            user = args[0];
            pass = args[1];

            // create our steamclient instance
            steamClient = new SteamClient();
            // create the callback manager which will route callbacks to function calls
            manager = new CallbackManager( steamClient );

            // get the steamuser handler, which is used for logging on after successfully connecting
            steamUser = steamClient.GetHandler<SteamUser>();

            // get the steam unified messages handler, which is used for sending and receiving responses from the unified service api
            steamUnifiedMessages = steamClient.GetHandler<SteamUnifiedMessages>();

            // we also want to create our local service interface, which will help us build requests to the unified api
            playerService = steamUnifiedMessages.CreateService<IPlayer>();


            // register a few callbacks we're interested in
            // these are registered upon creation to a callback manager, which will then route the callbacks
            // to the functions specified
            manager.Subscribe<SteamClient.ConnectedCallback>( OnConnected );
            manager.Subscribe<SteamClient.DisconnectedCallback>( OnDisconnected );

            manager.Subscribe<SteamUser.LoggedOnCallback>( OnLoggedOn );
            manager.Subscribe<SteamUser.LoggedOffCallback>( OnLoggedOff );

            // we use the following callbacks for unified service responses
            manager.Subscribe<SteamUnifiedMessages.ServiceMethodResponse>( OnMethodResponse );

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

            Console.WriteLine( "Successfully logged on!" );

            // now that we're logged onto Steam, lets query the IPlayer service for our badge levels

            // first, build our request object, these are autogenerated and can normally be found in the SteamKit2.Unified.Internal namespace
            CPlayer_GetGameBadgeLevels_Request req = new CPlayer_GetGameBadgeLevels_Request
            {
                // we want to know our 440 (TF2) badge level
                appid = 440,
            };

            // now lets send the request, this is done by building an expression tree with the IPlayer interface
            badgeRequest = playerService.SendMessage( x => x.GetGameBadgeLevels( req ) );

            // alternatively, the request can be made using SteamUnifiedMessages directly, but then you must build the service request name manually
            // the name format is in the form of <Service>.<Method>#<Version>
            steamUnifiedMessages.SendMessage( "Player.GetGameBadgeLevels#1", req );
        }

        static void OnLoggedOff( SteamUser.LoggedOffCallback callback )
        {
            Console.WriteLine( "Logged off of Steam: {0}", callback.Result );
        }

        static void OnMethodResponse( SteamUnifiedMessages.ServiceMethodResponse callback )
        {
            if ( callback.JobID != badgeRequest )
            {
                // always double check the jobid of the response to ensure you're matching to your original request
                return;
            }

            // and check for success
            if ( callback.Result != EResult.OK )
            {
                Console.WriteLine( $"Unified service request failed with {callback.Result}" );
                return;
            }

            // retrieve the deserialized response for the request we made
            // notice the naming pattern
            // for requests: CMyService_Method_Request
            // for responses: CMyService_Method_Response
            CPlayer_GetGameBadgeLevels_Response resp = callback.GetDeserializedResponse<CPlayer_GetGameBadgeLevels_Response>();

            Console.WriteLine( $"Our player level is {resp.player_level}" );

            foreach ( var badge in resp.badges )
            {
                Console.WriteLine( $"Badge series {badge.series} is level {badge.level}" );
            }

            badgeRequest = JobID.Invalid;

            // now that we've completed our task, lets log off
            steamUser.LogOff();
        }
    }
}
