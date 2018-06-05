using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

using Logging;
using ClientServer.Messages;

namespace ClientServer.Communication
{
    abstract public class ClientTCP : Loggable
    {
        private Socket _socket = null;
        private byte[] _buffer = null;
        private IFormatter formatter = new BinaryFormatter();
        Stream _stream = null;

        public string Ip { get => ((IPEndPoint)_socket.RemoteEndPoint).Address.ToString(); }
        public int Port { get => ((IPEndPoint)_socket.RemoteEndPoint).Port; }
        public bool Closing { get; private set; } = false;
        public int BufferSize { get; private set; } = 0;

        /// <summary>
        /// Initializes client connection
        /// Call startClient() to actually begin receiving
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="bufferSize"></param>
        /// <param name="log"></param>
        public ClientTCP(Socket soc, int bufferSize, string log) : base(log)
        {
            this._socket = soc;
            this._stream = new MemoryStream(_buffer);
            this._buffer = new byte[bufferSize];
        }

        /// <summary>
        /// begins listening to client requests
        /// Logging is performed internally
        /// </summary>
        public void startClient()
        {
            _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceviceCallback), _socket);
            Log("Connection started with " + this.Ip + ":" + this.Port);
            Closing = false;
        }

        private void ReceviceCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            try
            {
                int receivedSize = socket.EndReceive(ar);

                //Read Data
                _stream.Position = 0;
                byte[] databuffer = new byte[receivedSize];
                _socket.Receive(databuffer, receivedSize, 0);
                Array.Copy(_buffer, databuffer, receivedSize);

                //Get original type
                Message recMex = (Message)formatter.Deserialize(_stream);

                //Handle data
                HandleNewMessage(recMex);
            }
            catch (IOException e)
            {
                LogException("Error Reading from " + Ip + " due to error; " + e.ToString());
                Close();
            }
        }

        /// <summary>
        /// Writes message to client
        /// Logging is performed internally
        /// </summary>
        /// <param name="mex"></param>
        /// <exception cref="IOException">Generic IO exception</exception>
        public void Write(Message mex)
        {
            try
            {
                formatter.Serialize(_stream, mex);
                _stream.Flush();
                _socket.Send(_buffer, _buffer.Length, 0);
                _stream.Position = 0;
            }
            catch(IOException e)
            {
                LogException("Message to " + Ip + " failed with error: " + e.ToString());
                throw e;
            }
        }

        /// <summary>
        /// Closes connection to the client
        /// Logging is performed internally
        /// </summary>
        public void Close()
        {
            this.Closing = true;
            this._socket.Close();
            Log("Connection from " + this.Ip + " has been terminated");
        }

        /// <summary>
        /// Handles newly received message
        /// </summary>
        /// <param name="mex"></param>
        protected abstract void HandleNewMessage(Message mex);
    }
}
