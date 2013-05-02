using System;
using System.Configuration;
using System.ServiceModel;
using System.IO;

using log4net;
using log4net.Config;
using EQEmulator.Servers.ServerTalk;

namespace EQEmulator.Launchers
{
    class ZoneServerLauncher
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(ZoneServerLauncher));
        private static ServiceHost _svcHost = null;

        static void Main(string[] args)
        {
            int port = 0;
            if (args.Length != 1 || !int.TryParse(args[0], out port))
            {
                _log.FatalFormat("Incorrect ZoneServer command line. {0} args passed, expected one (port).", args.Length);
                Environment.Exit(1);
            }

            string svcBaseAddr = "net.tcp://localhost:" + args[0] + "/ZoneService/";

            // Load config info
            XmlConfigurator.Configure();
            SetLogging("FileAppender");

            try
            {
                EQEmulator.Servers.ZoneServer zoneSvr = new EQEmulator.Servers.ZoneServer(port);

                // Start the zone server and accompanying WCF listener(s)
                zoneSvr.Start();

                NetTcpBinding svcBinding = new NetTcpBinding();
                _svcHost = new ServiceHost(zoneSvr);
                _svcHost.Faulted += new EventHandler(svcHost_Faulted);
                _svcHost.UnknownMessageReceived += new EventHandler<UnknownMessageReceivedEventArgs>(svcHost_UnknownMessageReceived);
                _svcHost.AddServiceEndpoint(typeof(IZoneService), svcBinding, svcBaseAddr);
                _svcHost.Open();

                Console.Read();     // TODO: Change to read set of commands?
                _svcHost.Close();
                zoneSvr.Stop();
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
                _log.Fatal("Zone Server Socket Error", se);
                Environment.Exit(se.ErrorCode);
            }
            catch (Exception ex)
            {
                _log.Fatal("Unhandled Exception in Launcher.", ex);
            }
        }

        static void svcHost_UnknownMessageReceived(object sender, UnknownMessageReceivedEventArgs e)
        {
            _log.ErrorFormat("Communication error in Zone service: Unknown Message Recv. State: {0}, Message: {1}", _svcHost.State, e.Message);
        }

        static void svcHost_Faulted(object sender, EventArgs e)
        {
            _log.ErrorFormat("Fault detected in Zone WCF service. State: {0}", _svcHost.State);
        }

        static void SetLogging(string appenderName)
        {
            log4net.Repository.ILoggerRepository RootRep = log4net.LogManager.GetRepository();

            foreach (log4net.Appender.IAppender iApp in RootRep.GetAppenders())
            {
                if (iApp.Name.CompareTo(appenderName) == 0 && iApp is log4net.Appender.FileAppender)
                {
                    log4net.Appender.FileAppender fApp = (log4net.Appender.FileAppender)iApp;

                    Random rand = new Random();
                    string fileName = Path.GetFileNameWithoutExtension(fApp.File) + rand.Next() + Path.GetExtension(fApp.File);
                    fApp.File = Path.Combine(Path.GetDirectoryName(fApp.File), fileName);
                    fApp.ActivateOptions();
                }
            }
        }
    }
}
