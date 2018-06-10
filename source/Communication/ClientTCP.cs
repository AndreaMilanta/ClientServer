using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections.Generic;

using Logging;
using ClientServer.Messages;
using ClientServer.Exceptions;

namespace ClientServer.Communication
{
    public class ClientTCP : Loggable
    {
        // Defines the status of the client
        private enum Status
        {
            IDLE,
            SYNC,
            ASYNC,
            CLOSING
        };

        private bool _continuousASync = false;  // defines whether to keep on ASyinchorounusly read or stop at next received mex
        private int _failCount = 0;             // Counts the nuber of failure occured during communication
        private int _consecutiveFailCount = 0;  // Counts the number of consecutive failures
        private readonly int _maxFailures;
        private readonly int _maxConsecutiveFailures;

        public bool Receive { get; set; } = true;
        public bool Closed { get => (this._status == Status.CLOSING) ? true : false; }

        private Socket _socket = null;
        private byte[] _bufferIn = null;
        private byte[] _bufferOut = null;
        private IFormatter formatter = new BinaryFormatter();
        Stream _streamIn = null;
        Stream _streamOut = null;

        // Remote
        public string RemoteIp { get => ((IPEndPoint)_socket.RemoteEndPoint).Address.ToString(); }
        public int RemotePort { get => ((IPEndPoint)_socket.RemoteEndPoint).Port; }
        public string Remote { get => RemoteIp + ":" + RemotePort; }

        // Local
        public string LocalIp { get => ((IPEndPoint)_socket.LocalEndPoint).Address.ToString(); }
        public int LocalPort { get => ((IPEndPoint)_socket.LocalEndPoint).Port; }
        public string Local { get => LocalIp + ":" + LocalPort; }

        // Params
        private Status _status = Status.IDLE;
        public int BufferSize { get; private set; } = 0;

        // Delegates
        public delegate void MessageHandler(Message mex);
        public MessageHandler HandleASyncMessage = null;
        public MessageHandler HandleSyncMessage = null;


        /// <summary>
        /// Initializes client connection
        /// Call startClient() to actually begin receiving
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="bufferSize"></param>
        /// <param name="log"></param>
        public ClientTCP(Socket soc, int bufferSize, int maxFailures, int maxContFailures, string log) : base(log)
        {
            this._maxFailures = maxFailures;
            this._maxConsecutiveFailures = maxContFailures;
            this._socket = soc;
            this.BufferSize = bufferSize;
            this._bufferIn = new byte[BufferSize];
            this._bufferOut = new byte[BufferSize];
            _bufferIn.Initialize();
            _bufferOut.Initialize();
            this._streamIn = new MemoryStream(_bufferIn);
            this._streamOut = new MemoryStream(_bufferOut);
            Log("New Connection to " + this.Remote);
        }

        // SYNC STUFF

        /// <summary>
        /// reads Synchronously
        /// Exceptions are NOT handled
        /// </summary>
        /// <param name="timeout_s"></param>
        public void ReadSync(float timeout_s)
        {
            if (_status == Status.CLOSING)
                throw new ClientHasClosedException();
            if (_status == Status.ASYNC)
                throw new ASynchronousReadingInProcessException();
            _status = Status.IDLE;
            _socket.ReceiveTimeout = (int)(timeout_s * 1000);
            try
            {
                _socket.Receive(_bufferIn, BufferSize, 0);
            }
            catch(Exception ex)
            {
                LogError("Error Sync Reading from " + this.Remote + " with Error: ");
                throw ex;
            }
            HandleSyncMessage(GetMessage());
        }

        private Message GetMessage()
        {
            _streamIn.Position = 0;
            try
            {
                Message mex = (Message)formatter.Deserialize(_streamIn);
                _consecutiveFailCount = 0;
                return mex;
            }
            catch (Exception)
            {
                LogError(new InvalidMessageException().ToString());
                _failCount++;
                _consecutiveFailCount++;
                if (_consecutiveFailCount >= _maxConsecutiveFailures)
                {
                    LogError("Too many consecutive failures");
                    this.Close();
                }
                if (_failCount >= _maxFailures)
                {
                    LogError("Too many failures");
                    this.Close();
                }
                throw new InvalidMessageException();
            }
        }

        /// <summary>
        /// begins listening to client requests asynchronously
        /// Logging is performed internally
        /// </summary>
        public void ReadASync(bool continuous)
        {
            if (_status == Status.CLOSING)
                throw new ClientHasClosedException();
            this._continuousASync = continuous;
            if (_status == Status.ASYNC)                // if already reading, keep reading
                return;
            _status = Status.ASYNC;
            try
            {
                _socket.BeginReceive(_bufferIn, 0, BufferSize, SocketFlags.None, new AsyncCallback(ReceviceCallback), _socket);
            }
            catch (ObjectDisposedException)
            {
                this.Close();
            }
        }

        private void ReceviceCallback(IAsyncResult ar)
        {
            if (_status == Status.CLOSING)                  // Exceptions cannot be handled asynchronously
                return;                                 
            Socket socket = (Socket)ar.AsyncState;
            try
            {
                int receivedSize = socket.EndReceive(ar);

                //Read Data if available, otherwise restart listening
                if (receivedSize > 0)
                    HandleASyncMessage(GetMessage());
                else
                    ReadASync(_continuousASync);
            }
            catch (ObjectDisposedException)
            {
                this.Close();
                return;
            }
            catch (Exception ex)
            {
                LogError("Error ASync Reading from " + this.Remote + " due to error; " + ex.ToString());
                this.Close();
                return;
            }
            _status = Status.IDLE;
            if (_continuousASync)
                    ReadASync(_continuousASync);
        }

        /// <summary>
        /// Writes message to client
        /// Logging is performed internally
        /// </summary>
        /// <param name="mex"></param>
        /// <exception cref="IOException">Generic IO exception</exception>
        public void Write(Message mex)
        {
            if (_status == Status.CLOSING)
                throw new ClientHasClosedException();
            try
            {
                formatter.Serialize(_streamOut, mex);
                _streamOut.Flush();
                int sent = _socket.Send(_bufferOut, BufferSize, 0);
                _streamOut.Position = 0;
                _bufferOut.Initialize();
            }
            catch(IOException e)
            {
                LogError("Message to " + this.Remote + " failed with error: " + e.ToString());
                this.Close();
            }
            catch(SocketException e)
            {
                LogError(this.Remote + " failed with error: " + e.ToString());
                this.Close();
            }
        }

        /// <summary>
        /// Closes connection to the client
        /// Logging is performed internally
        /// </summary>
        public void Close()
        {
            this._status = Status.CLOSING;
            try
            {
                Log("Connection from " + this.Remote + " has been terminated");
                this._socket.Close();
            }
            catch (ObjectDisposedException) { }
        }
    }
}
