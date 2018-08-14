using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.Varlink;

namespace Org.Varlink
{
    class GetInfoResult
    {
        public string vendor
        {
            get;
            set;
        }

        public string product
        {
            get;
            set;
        }

        public string version
        {
            get;
            set;
        }

        public string url
        {
            get;
            set;
        }

        public string[] interfaces
        {
            get;
            set;
        }
    }

    class GetInterfaceDescriptionResult
    {
        public string description
        {
            get;
            set;
        }
    }

    class GetInterfaceDescriptionArgs
    {
        public string @interface
        {
            get;
            set;
        }
    }

    class Service : IDisposable
    {
        public const string InterfaceName = "org.varlink.service";
        private readonly IConnection _conn;
        public Service(string address)
        {
            _conn = new Connection(address);
        }

        public Task<GetInfoResult> GetInfoAsync()
        {
            return _conn.CallAsync<GetInfoResult>("org.varlink.service.GetInfo", GetErrorParametersType);
        }

        public Task<GetInterfaceDescriptionResult> GetInterfaceDescriptionAsync(GetInterfaceDescriptionArgs args)
        {
            return _conn.CallAsync<GetInterfaceDescriptionResult>("org.varlink.service.GetInterfaceDescription", GetErrorParametersType, args);
        }

        private static System.Type GetErrorParametersType(string args)
        {
            return null;
        }

        public void Dispose()
        {
            _conn.Dispose();
        }
    }
}