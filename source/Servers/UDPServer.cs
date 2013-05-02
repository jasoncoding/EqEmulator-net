using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using EQEmulator.Servers.Internals.Packets;
using log4net;

namespace EQEmulator.Servers
{
    public abstract class UDPServer
    {
        protected static readonly ILog _log = LogManager.GetLogger(typeof(UDPServer));
        protected int _port;
        private UdpClient _client;              // UDP socket
        private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private int _rwOpCount = 0;             // number of outstanding operations on socket, used for clean thread exits
        private bool _shutdownFlag = true;      // sync'd through the ReaderWriterLock

        internal abstract void PacketReceived(BasePacket packet);     // implement in derived class to process incoming packets
        internal abstract void ServerStarting();    // acts as a pre-startup event
        internal abstract void ServerStarted();     // acts as a startup event
        internal abstract void ServerStopping();    // acts as a pre-shutdown event
        internal abstract void ServerStopped();     // acts as a shutdown event

        public UDPServer(int port)
        {
            this._port = port;
        }

        public bool IsRunning
        {
            get { return !_shutdownFlag; }
        }

        /// <summary>
        /// Starts the EQEmulator Login Server.
        /// <remarks>Only time an exception is thrown is if there is a problem starting the UdpClient instance -
        /// most likely due to port in use.</remarks>
        /// </summary>
        public void Start()
        {
            if (_shutdownFlag)
            {
                // create socket - no exception handling here because failure here is bad, so let the caller see it and exit
                // TODO: May want to throw a custom exception instead of a socket exception?
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, _port);
                _client = new UdpClient(localEndPoint);
                _log.Info("Server Started.");

                _shutdownFlag = false;
                ServerStarting();
                AsyncBeginReceive();
                ServerStarted();
            }
        }

        public void Stop()
        {
            if (!_shutdownFlag)
            {
                _log.Info("Beginning Server shutdown.");
                ServerStopping();
                _rwLock.EnterWriteLock();   // blocks until all readers have exited their locks
                _shutdownFlag = true;
                _client.Close();
                _rwLock.ExitWriteLock();

                // wait for other pending ops to complete before exiting
                while (_rwOpCount > 0)
                    Thread.Sleep(1);

                ServerStopped();

                _log.Info("Server shutdown complete.");
            }
        }

        protected void AsyncBeginReceive()
        {
            Thread.Sleep(0);    // Let's try this to see if yielding here can be good
            _rwLock.EnterReadLock();  // ensure someone doesn't close the socket while we're at work here

            if (!_shutdownFlag)  // make sure we haven't been shutdown
            {
                while (true)
                {
                    Interlocked.Increment(ref _rwOpCount);  // increment count of pending ops

                    try
                    {
                        _client.BeginReceive(new AsyncCallback(this.AsyncEndReceive), null);
                        break;
                    }
                    catch (SocketException se)
                    {
                        //_log.Error("Error receiving data from client.", se);
                        Interlocked.Decrement(ref _rwOpCount);  // operation is void - decrement pending op count
                    }
                }
            }

            _rwLock.ExitReadLock();    // release reader lock on socket
        }

        protected void AsyncEndReceive(IAsyncResult ar)
        {
            _rwLock.EnterReadLock();  // ensure someone doesn't close the socket while we're at work here

            if (!_shutdownFlag)
            {
                IPEndPoint remoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] recvBuffer = {0x00};

                try
                {
                    recvBuffer = _client.EndReceive(ar, ref remoteIPEndPoint);
                }
                catch (SocketException se)
                {
                    _log.Error("Error receiving data from client.", se);
                    // TODO: handle the case of the client closing the socket
                }
                finally
                {
                    Interlocked.Decrement(ref _rwOpCount);  // decrement pending op count - success or failure
                    _rwLock.ExitReadLock();                 // done with socket
                    AsyncBeginReceive();    // keep the server churning
                }

                //_log.Debug(string.Format("Handling client at {0}", remoteIPEndPoint));
                //_log.Debug(string.Format("received {0} bytes.", recvBuffer.Length));
                //_log.Debug(string.Format("Bytes Recv Dump: {0}", BitConverter.ToString(recvBuffer)));

                if (recvBuffer.Length > 1)
                    PacketReceived(new BasePacket(remoteIPEndPoint, recvBuffer));
            }
            else
            {
                // socket is closing
                Interlocked.Decrement(ref _rwOpCount);
                _rwLock.ExitReadLock();
            }
        }

        internal void AsyncBeginSend(BasePacket packet)
        {
            _rwLock.EnterReadLock();
            if (!_shutdownFlag)
            {
                try
                {
                    Interlocked.Increment(ref _rwOpCount);
                    //_log.DebugFormat("Bytes Sent Dump: {0}", BitConverter.ToString(packet.RawPacketData));
                    _client.BeginSend(packet.RawPacketData, packet.RawPacketData.Length, packet.ClientIPE, new AsyncCallback(AsyncEndSend), null);
                }
                catch (SocketException se)
                {
                    _log.Error("Error sending data to client.", se);
                    Interlocked.Decrement(ref _rwOpCount);  // operation is void - decrement pending op count
                }
            }

            _rwLock.ExitReadLock();
        }

        protected void AsyncEndSend(IAsyncResult ar)
        {
            _rwLock.EnterReadLock();
            if (!_shutdownFlag)
            {
                try
                {
                    int bytesSent = _client.EndSend(ar);
                    //_log.Debug(string.Format("Successfully sent {0} bytes to the client.", bytesSent));
                }
                catch (SocketException se)
                {
                    _log.Error("Error completing a send to the client.", se);
                }
            }

            Interlocked.Decrement(ref _rwOpCount);
            _rwLock.ExitReadLock();
        }
    }
}
