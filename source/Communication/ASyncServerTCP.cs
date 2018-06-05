using System;
using System.Net;
using System.Net.Sockets;

using Logging;

namespace ClientServer.Communication
{
    abstract public class ASyncServerTCP : Loggable
    {
        protected Socket _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public string Name { get; protected set; }
        public int MaxUsers { get; protected set; }
        public int Port { get => ((IPEndPoint)_serverSocket.LocalEndPoint).Port; }
        public string Ip { get => ((IPEndPoint)_serverSocket.LocalEndPoint).Address.ToString(); }

        public ASyncServerTCP(string name, string log) : base(log)
        {
            this.Name = name;
            Log("Server " + name + "has been created");
        }

        /// <summary>
        /// Setup server
        /// Logging is performed internally
        /// </summary>
        /// <param name="port"></param>
        /// <param name="maxUsers"></param>
        /// <exception cref="SocketException">Generic socket exception. Maybe port busy</exception>
        public void Setup(int port, int maxUsers)
        {
            try
            {
                this.MaxUsers = maxUsers;
                _serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                Log("Server successfully setup on port " + port);
            }
            catch(SocketException e)
            {
                LogException("Server setup on port " + port + " failed with error: " + e.ToString());
                throw e;
            }
        }

        /// <summary>
        /// Start server: begin accepting requests
        /// Logging is performed internally
        /// </summary>
        public void Start()
        {
            try
            {
                _serverSocket.Listen(this.MaxUsers);
                _serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
                Log("Server successfully started");
            }
            catch(SocketException e)
            {
                Log("Server could not start due to error: " + e.ToString());
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            Socket socket = _serverSocket.EndAccept(ar);
            _serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
            HandleNewConnection(socket);
        }

        protected abstract void HandleNewConnection(Socket newSocket);
    }
}
