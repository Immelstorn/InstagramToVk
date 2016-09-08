using System;
using System.Linq;
using System.Xml;
using System.Collections.Specialized;
using Newtonsoft.Json;

namespace InstagramToVK
{
    class VKAPI
    {
        #region vars
        private string _accessToken = "", ResponseUri, Stream;
        private XmlDocument _result = new XmlDocument();
        private NameValueCollection _qs = new NameValueCollection();
        private static VKAPI _instance;
        private static object _syncLock = new object();
        private NetworkClass _network;
        #endregion

        private VKAPI(string accessToken)
        {
            _network = NetworkClass.GetNetworkClass();
            _accessToken = accessToken;
        }

        //дас ист синглтооон!
        public static VKAPI GetVkApi(string accessToken)
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    if (_instance == null)
                    {
                        _instance = new VKAPI(accessToken);
                    }
                }
            }
            return _instance;
        }

        private XmlDocument ExecuteCommand(string name, NameValueCollection qs)
        {
            var url = String.Format("https://api.vk.com/method/{0}.xml?access_token={1}&{2}", name, _accessToken, String.Join("&", from item in qs.AllKeys select item + "=" + qs[item]));
            _network.GetRequestAndResponse(url, out ResponseUri, out Stream);
            _result.LoadXml(Stream);
            _qs = new NameValueCollection();
            return _result;
        }

        public XmlDocument WallGet(int count, int offset)
        {
            _qs["count"] = count.ToString();
            _qs["filter"] = "owner";
            _qs["offset"] = offset.ToString();
            return ExecuteCommand("wall.get", _qs);
        }

        public XmlDocument WallPostFoto(int uid, InstaFoto foto)
        {
            // парсим урл для загрузки
            var result = GetWallUploadServer(uid);
            var uploadUrl = _network.GetDataFromXmlNode(result.SelectSingleNode("/response/upload_url"));

            //загружаем
            var uploadedPhoto = UploadFoto(uploadUrl, foto.Filename);

            //сохраняем
            result = SaveWallPhoto(uploadedPhoto);

            //парсим данные сохраненного фото
            uploadedPhoto.SavedID = _network.GetDataFromXmlNode(result.SelectSingleNode("/response/photo/id"));
            uploadedPhoto.SavedPID = _network.GetDataFromXmlNode(result.SelectSingleNode("/response/photo/pid"));
            uploadedPhoto.SavedOwnerID = _network.GetDataFromXmlNode(result.SelectSingleNode("/response/photo/owner_id"));

            //кодируем теги в урл-формате
            if (foto.Text.Contains("#"))
            {
                foto.Text = foto.Text.Replace("#", "%23");
            }

            //постим на стену
            _qs["message"] = foto.Text + " " + foto.Link;
            _qs["attachments"] = uploadedPhoto.SavedID;
            return ExecuteCommand("wall.post", _qs);
        }

        private XmlDocument SaveWallPhoto(VkPhoto photo)
        {
            _qs["server"] = photo.Server;
            _qs["photo"] = photo.Id;
            _qs["hash"] = photo.Hash;
            return ExecuteCommand("photos.saveWallPhoto", _qs);
        }

        private XmlDocument GetWallUploadServer(int uid)
        {
            _qs["uid"] = uid.ToString();
            return ExecuteCommand("photos.getWallUploadServer", _qs);
        }

        private VkPhoto UploadFoto(string uploadUrl, string filename)
        {
            //аплоадим фото
            Stream = _network.HttpUploadFile(uploadUrl, filename, "photo", "image/jpeg");

            //парсим ответ на предмет сервера, айди и хэша

            _result = JsonConvert.DeserializeXmlNode(Stream, "baseprop");

            return new VkPhoto
                              {
                                  Server = _network.GetDataFromXmlNode(_result.SelectSingleNode("/baseprop/server")),
                                  Id = _network.GetDataFromXmlNode(_result.SelectSingleNode("/baseprop/photo")),
                                  Hash = _network.GetDataFromXmlNode(_result.SelectSingleNode("/baseprop/hash"))
                              };
        }
    }
}
