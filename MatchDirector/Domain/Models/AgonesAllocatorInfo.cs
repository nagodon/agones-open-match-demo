using System;
using System.Text;

namespace MatchDirector.Domain.Models
{
    public class AgonesAllocatorInfo
    {
        public string Ip { get; set; }
        public string ClientKey { get; set; }
        public string ClientCert { get; set; }
        public string TlsCert { get; set; }

        public string RawClientKey()
        {
            return FromBase64String(ClientKey);
        }

        public string RawClientCert()
        {
            return FromBase64String(ClientCert);
        }

        public string RawTlsCert()
        {
            return FromBase64String(TlsCert);
        }

        private string FromBase64String(string base64String)
        {
            return new UTF8Encoding().GetString(Convert.FromBase64String(base64String));
        }
    }
}
