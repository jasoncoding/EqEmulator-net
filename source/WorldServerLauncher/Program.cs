using System;
using System.Configuration;
using System.ServiceModel;

using EQEmulator.Launchers.Properties;
using log4net;
using log4net.Config;

namespace EQEmulator.Launchers
{
    class WorldServerLauncher
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(WorldServerLauncher));
        private static ServiceHost _svcHost = null;
        private static EQEmulator.Servers.WorldServer _worldSvr = null;

        static void Main(string[] args)
        {
            // TODO: Parse cmd line args?

            // Load config info
            XmlConfigurator.Configure();

            _worldSvr = new EQEmulator.Servers.WorldServer(Settings.Default.WorldId, Settings.Default.Port);
            
            try
            {
                // Start the world server and accompanying WCF listener(s)
                _worldSvr.Start();   // Kick off the world server
                StartServiceHost();

                Console.Read();     // TODO: Change to read set of commands?
                _worldSvr.Stop();
                _svcHost.Close();
            }
            catch (CommunicationException ce)    // Specific fault handlers go before the CommunicationException handler
            {
                _log.Error("Communication error in WCF service.", ce);
            }
            catch (TimeoutException te)
            {
                _log.Error("Timeout detected in launcher.", te);
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // TODO: distinguish between a critical startup exception and a general running exception?
                _log.Fatal("World Server Socket Error", se);
                Environment.Exit(se.ErrorCode);
            }
            catch (Exception ex)
            {
                _log.Fatal("Unhandled Exception in Launcher.", ex);
            }
        }

        static void svcHost_UnknownMessageReceived(object sender, UnknownMessageReceivedEventArgs e)
        {
            _log.ErrorFormat("Communication error in WCF service: Unknown Message Recv. State: {0}, Message: {1}", _svcHost.State, e.Message);
        }

        static void svcHost_Faulted(object sender, EventArgs e)
        {
            _log.ErrorFormat("Fault detected in World WCF service. State: {0}", _svcHost.State);
            
            // Abort and then restart the service host
            _svcHost.Abort();
            StartServiceHost();
        }

        static void StartServiceHost()
        {
            _svcHost = new ServiceHost(_worldSvr);
            _svcHost.Faulted += new EventHandler(svcHost_Faulted);
            _svcHost.UnknownMessageReceived += new EventHandler<UnknownMessageReceivedEventArgs>(svcHost_UnknownMessageReceived);
            _svcHost.Open();
        }
    }
}
