using System;
using System.IO;

using Logging;
using ClientServer.Messages;
using ClientServer.Exceptions;

namespace ClientServer.Communication
{
    public abstract class ClientWrapper : Loggable
    {
        protected ClientTCP Client { get; set; }
        public bool Closed { get => Client.Closed; }

        public ClientWrapper(ClientTCP client, string logger) : base(logger)
        {
            this.Client = client;
        }

        public ClientWrapper(string logger) : base(logger)
        {
            this.Client = null;
        }

        public void ReadASync(bool continuous)
        {
            Client.ReadASync(continuous);
        }

        public void ReadSync(int timeout_s)
        {
            Client.ReadSync(timeout_s);
        }

        /// <summary>
        /// Send Message to Client
        /// </summary>
        /// <param name="mex"></param>
        /// <exception cref="ClientHasClosedException"></exception>
        public void Write(Message mex)
        {
            try
            {
                Client.Write(mex);
            }
            catch(IOException ex)
            {
                LogException(ex);
                this.Close();
                throw new ClientHasClosedException();
            }
        }

        /// <summary>
        /// Closes communication
        /// </summary>
        /// <param name="notification">optional notification message to send before closing</param>
        public void Close(Message notification = null)
        {
            try
            {
                if (notification != null)
                    Client.Write(notification);
                Client.Close();
            }
            catch(ClientHasClosedException) { }
        }
    }
}
