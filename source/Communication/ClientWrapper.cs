using System.IO;

using Logging;
using ClientServer.Messages;

namespace ClientServer.Communication
{
    public abstract class ClientWrapper : Loggable
    {
        protected ClientTCP Client { get; set; }

        public ClientWrapper(ClientTCP client, string logger) : base(logger)
        {
            this.Client = client;
        }

        public ClientWrapper(string logger) : base(logger)
        {
            this.Client = null;
        }

        public void ReadAsync()
        {
            Client.ReadAsync();
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
            }
        }

        public void Close()
        {
            Client.Close();
        }
    }
}
