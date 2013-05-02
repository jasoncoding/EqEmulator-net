using System;
using System.Configuration;

using EQEmulator.Launchers.Properties;
using log4net;
using log4net.Config;

namespace EQEmulator.Launchers
{
    class LoginServerLauncher
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(LoginServerLauncher));

        static void Main(string[] args)
        {
            // TODO: Parse cmd line args?

            // Load config info
            XmlConfigurator.Configure();
            int port = Settings.Default.Port;
            EQEmulator.Servers.LoginServer loginSvr = new EQEmulator.Servers.LoginServer(port);

            try
            {
                loginSvr.Start();   // Kick off the login server
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // TODO: distinguish between a critical startup exception and a general running exception?
                _log.Fatal("Error during startup of Login Server", se);
                Environment.Exit(se.ErrorCode);
            }
            catch (Exception ex)
            {
                _log.Fatal("Unhandled Exception in Launcher.", ex);
            }

            Console.Read();     // TODO: Change to read set of commands?
            loginSvr.Stop();
        }
    }
}