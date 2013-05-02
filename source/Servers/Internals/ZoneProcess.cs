using System;
using System.Diagnostics;
using System.ServiceModel;

using EQEmulator.Servers.ServerTalk;

namespace EQEmulator.Servers.Internals
{
    internal class ZoneProcess
    {
        private short _port;
        private ChannelFactory<ServerTalk.IZoneService> _svcClient;
        private Process _process;
        private ushort? _zoneId = null;       // Null when a zone not loaded, set when booting a zone

        public ZoneProcess(short port, Process process, ChannelFactory<ServerTalk.IZoneService> svcClient)
        {
            _port = port;
            _process = process;
            _svcClient = svcClient;
        }

        public short Port
        {
            get { return _port; }
        }

        public ServerTalk.IZoneService ZoneService
        {
            get
            {
                if (_svcClient.State != CommunicationState.Opened)
                    _svcClient.Open();

                IZoneService svc = _svcClient.CreateChannel();
                ((IContextChannel)svc).OperationTimeout = new TimeSpan(0, 5, 0);
                return svc;
            }
        }

        public Process Process
        {
            get { return _process; }
        }

        public ushort? ZoneId
        {
            get { return _zoneId; }
            set { _zoneId = value; }
        }
    }
}
