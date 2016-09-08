using System;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;

namespace InstagramToVK
{
    class NetworkClass
    {
        private static NetworkClass _instance;
        private static object _syncLock = new object();
        private static CookieContainer _cookieContainer = new CookieContainer();

        private NetworkClass() { }

        //синглтон
        public static NetworkClass GetNetworkClass()
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    if (_instance == null)
                    {
                        _instance = new NetworkClass();
                    }
                }
            }
            return _instance;
        }

        public void GetRequestAndResponse(string uri, out string responseUri, out string stream, string method = "GET")
        {
            var request = WebRequest.Create(uri) as HttpWebRequest;
            if (request != null)
            {
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "Mozilla/5.0 (Macintosh; U; Intel Mac OS X 10_6_7; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Chrome/10.0.648.205 Safari/534.16";
                request.CookieContainer = _cookieContainer;
                request.Method = method;
            }
            stream = "";
            responseUri = "";
            if (request != null)
                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    if (response != null)
                    {
                        using (var receiveStream = response.GetResponseStream())
                        {
                            if (receiveStream != null)
                            {
                                var readStream = new StreamReader(receiveStream, Encoding.UTF8);
                                stream = readStream.ReadToEnd();
                            }
                        }
                        responseUri = response.ResponseUri.ToString();
                    }
                }
        }

        public string HttpUploadFile(string url, string file, string paramName, string contentType)
        {
            var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            var boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");


            var wr = (HttpWebRequest)WebRequest.Create(url);
            wr.CookieContainer = new CookieContainer();
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = CredentialCache.DefaultCredentials;

            using (var rs = wr.GetRequestStream())
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);

                const string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
                var header = string.Format(headerTemplate, paramName, file, contentType);
                var headerbytes = Encoding.UTF8.GetBytes(header);
                rs.Write(headerbytes, 0, headerbytes.Length);

                using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        rs.Write(buffer, 0, bytesRead);
                    }
                }

                var trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                rs.Write(trailer, 0, trailer.Length);
            }
            WebResponse wresp = null;
            var s = "";
            try
            {
                wresp = wr.GetResponse();
                var stream2 = wresp.GetResponseStream();
                if (stream2 != null)
                {
                    var reader2 = new StreamReader(stream2);
                    @s = reader2.ReadToEnd();
                }
            }
            catch (Exception)
            {
                if (wresp != null)
                {
                    wresp.Close();
                }
            }
            return @s;
        }

        public string GetDataFromXmlNode(XmlNode input)
        {
            if (input == null || String.IsNullOrEmpty(input.InnerText))
            {
                return "";
            }
            return input.InnerText;
        }
    }
}
