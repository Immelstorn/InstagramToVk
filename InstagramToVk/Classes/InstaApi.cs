using System;
using System.Linq;
using System.Collections.Specialized;
using System.Xml;
using Newtonsoft.Json;

namespace InstagramToVK
{
    class InstaApi
    {
        #region vars
        private readonly string
            AccessToken = InstagramToVK.Properties.Resources.instagramAccessToken,
            MyId = "";
        private string _responseUri = "", _stream = "";
        private XmlDocument _result = new XmlDocument();
        private NameValueCollection _qs = new NameValueCollection();
        private static InstaApi _instance;
        private static object _syncLock = new object();
        private NetworkClass _network;
        #endregion

        private InstaApi()
        {
            _network = NetworkClass.GetNetworkClass();
            MyId = AccessToken.Remove(AccessToken.IndexOf('.'));
        }

        public static InstaApi GetInstaApi()
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    if (_instance == null)
                    {
                        _instance = new InstaApi();
                    }
                }
            }
            return _instance;
        }

        private XmlDocument ExecuteCommand(string name, NameValueCollection qs)
        {
            var url = String.Format("https://api.instagram.com/v1/{0}?access_token={1}&{2}", name, AccessToken, String.Join("&", from item in qs.AllKeys select item + "=" + qs[item]));
            _network.GetRequestAndResponse(url, out _responseUri, out _stream);
            _result = JsonConvert.DeserializeXmlNode(_stream, "baseprop");
            _qs = new NameValueCollection();
            return _result;
        }

        public XmlDocument GetRecentMedia(int count)
        {
            _qs["count"] = count.ToString();
            return ExecuteCommand(string.Format("users/{0}/media/recent/", MyId), _qs);
        }

        public XmlDocument GetRecentMedia(int count, string maxId)
        {
            _qs["count"] = count.ToString();
            _qs["max_id"] = maxId;
            return ExecuteCommand(string.Format("users/{0}/media/recent/", MyId), _qs);
        }

    }
}
