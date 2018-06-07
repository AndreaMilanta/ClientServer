using System;

namespace ClientServer.Exceptions
{
    public class InvalidMessageException : Exception
    {
        public override string ToString()
        {
            return "Invalid Message receivd";
        }
    }
}
