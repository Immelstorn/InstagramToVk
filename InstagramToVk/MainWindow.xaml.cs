using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Net;
using System.Collections;
using System.IO;
using System.Xml;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace InstagramToVK
{
    public partial class MainWindow
    {
        #region vars
        private NotifyIcon _myNotifyIcon;
        private readonly int UserId = int.Parse(Properties.Resources.vkComUserId);
        private readonly string AccessToken = Properties.Resources.vkComAccessToken;
        private string
            _fileName = "",
            _homeDir = System.Windows.Forms.Application.ExecutablePath;
        private DateTime _timeOfStartSleeping = DateTime.Now;
        private VKAPI _myVk;
        private ContextMenu _contextMenu = new ContextMenu();
        private MenuItem
            _menuItemExit = new MenuItem(),
            _menuItemTry = new MenuItem(),
            _menuItemStop = new MenuItem();
        private object _locker = new object();
        private InstaApi _instagram = InstaApi.GetInstaApi();
        private NetworkClass _network;
        private Thread _thr;
        private System.Drawing.Icon _iconWait, _iconWork;
        private DirectoryInfo _dir;
        private delegate void LastImageDelegate();
        private Logs _logs;
        #endregion

        public MainWindow()
        {
            _homeDir = _homeDir.Substring(0, _homeDir.Length - 17);
            _dir = new DirectoryInfo(_homeDir + @"fotos/");
            if (!(_dir.Exists))
            {
                _dir.Create();
            }
            _logs = Logs.GetLogsClass();
            _network = NetworkClass.GetNetworkClass();
            InitializeComponent();
            _myNotifyIcon = new NotifyIcon();
            _iconWait = Properties.Resources.vk1;
            _iconWork = Properties.Resources.vk2;
            Icon = Imaging.CreateBitmapSourceFromHIcon(Properties.Resources.vk1.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            _myNotifyIcon.Icon = _iconWait;
            _myNotifyIcon.MouseDoubleClick += MyNotifyIconMouseDoubleClick;
            _myNotifyIcon.MouseMove += MyNotifyIconMouseMove;
            _myNotifyIcon.Visible = true;

            _contextMenu.MenuItems.AddRange(new[] { _menuItemExit, _menuItemTry, _menuItemStop });

            _menuItemExit.Index = 2;
            _menuItemExit.Text = "Закрыть";
            _menuItemExit.Click += MenuItemExitClick;

            _menuItemStop.Index = 1;
            _menuItemStop.Text = "Остановить";
            _menuItemStop.Click += MenuItemStopClick;

            _menuItemTry.Index = 0;
            _menuItemTry.Text = "Дернуть";
            _menuItemTry.Click += MenuItemTryClick;

            _myNotifyIcon.ContextMenu = _contextMenu;

        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            LastImageInFolder();
            _myVk = VKAPI.GetVkApi(AccessToken);
        }

        #region Buttons
        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            if (_thr != null)
            {
                _thr.Abort();
            }
            _myNotifyIcon.Visible = false;
            Close();
        }

        private void BtnDoClick(object sender, RoutedEventArgs e)
        {
            //WindowState = WindowState.Minimized;
            MainMethod();
        }
        #endregion

        private void TimeOutForThread()
        {
            try
            {
                while (true)
                {
                    _timeOfStartSleeping = DateTime.Now;

                    _logs.WriteLog("log.txt", "SearchNewFotosAndPostToWallIfExists");
                    SearchNewFotosAndPostToWallIfExists();

                    _timeOfStartSleeping = DateTime.Now;
                    Dispatcher.BeginInvoke(new LastImageDelegate(LastImageInFolder));

                    _logs.WriteLog("log.txt", "Sleeping 1 hour \n");
                    Thread.Sleep(TimeSpan.FromHours(1));
                }
            }
            catch (Exception e)
            {
                _logs.WriteLog("log.txt", "Sleeping after exception 1 hour\n" + e.Message);
                MainAfterException();
            }
        }

        private void MainAfterException()
        {
             Thread.Sleep(TimeSpan.FromMinutes(60));
            _thr = new Thread(TimeOutForThread) { Name = "Main thread after Exception" };
            _thr.Start();
        }

        private void MainMethod()
        {
            _thr = new Thread(TimeOutForThread) { Name = "Main thread for this shit" };
            _thr.Start();
            btnDo.IsEnabled = false;
        }

        #region Other shit

        private void LastImageInFolder()
        {
            //рисуем последнюю обработанную фотку из инстаграмма в форме
            List<FileInfo> filesjpg = _dir.GetFiles().Where(t => t.Name.Contains(".jpg")).ToList();

            if (filesjpg.Count > 0)
            {
                filesjpg.Sort((f1, f2) => f1.LastWriteTime.CompareTo(f2.LastWriteTime));
                imgLastImage.Source =
                    new BitmapImage(new Uri(string.Format(@"{0}\{1}", _dir, filesjpg[filesjpg.Count - 1]),
                                            UriKind.RelativeOrAbsolute));
            }
        }

        private void SearchNewFotosAndPostToWallIfExists()
        {
            _myNotifyIcon.Icon = _iconWork;

            _logs.WriteLog("log.txt", "Ищем новые фотки");
            var listOfNewFotos = SearchForNewFotos();

            if (listOfNewFotos.Count != 0)
            {
                _logs.WriteLog("log.txt", "нашли и лист не пустой.");
                _logs.WriteLog("log.txt", "начинаем постить их все подряд");
                for (var i = listOfNewFotos.Count - 1; i >= 0; i--)
                {
                    _myVk.WallPostFoto(UserId, listOfNewFotos[i]);
                }

                //похвастались этим в балуне
                _myNotifyIcon.ShowBalloonTip(10000,
                    "New foto(s) posted", string.Format("{0} fotos has been posted to VK", listOfNewFotos.Count()), ToolTipIcon.Info);
            }
            _myNotifyIcon.Icon = _iconWait;

        }

        private List<InstaFoto> SearchForNewFotos()
        {
            var count = 0;
            var noNewFoto = false;
            var lastInstaFotos = new List<InstaFoto>();

            _logs.WriteLog("log.txt", "выкоыряли последний пост с инстафото из вконтакта");
            var lastVKInstaPost = SearchForInstaPost();

            _logs.WriteLog("log.txt", "выковыряли последнюю фотку из инстаграма");
            var lastXMLInstaFoto = _instagram.GetRecentMedia(1);

            while (lastXMLInstaFoto == null)
            {
                _logs.WriteLog("log.txt", "lastXMLInstaFoto is null ==> sleeping 1 hour and try again");
                Thread.Sleep(TimeSpan.FromHours(1));
                lastXMLInstaFoto = _instagram.GetRecentMedia(1);
            }

            _logs.WriteLog("log.txt", "сделали из ХМЛ инстафото и добавили  лист");
            lastInstaFotos.Add(GetInstaFotoFromXML(lastXMLInstaFoto));

            if (lastVKInstaPost != null)
            {
                _logs.WriteLog("log.txt", "сравнили фотки по ссылке");
                if (!lastVKInstaPost.Text.Contains(lastInstaFotos[0].Link.Replace("http://instagram.com/p/","")))
                {
                    _logs.WriteLog("log.txt", "фотки не было - проверям предыдущую фотку");
                    while (!noNewFoto)
                    {
                        var prevInstaFoto = _instagram.GetRecentMedia(1, lastInstaFotos[count].NextMaxId);
                        lastInstaFotos.Add(GetInstaFotoFromXML(prevInstaFoto));
                        count++;
                        _logs.WriteLog("log.txt", count.ToString());
                        if (lastVKInstaPost.Text.Contains(lastInstaFotos[count].Link.Replace("http://instagram.com/p/", "")))
                        {
                            _logs.WriteLog("log.txt", lastInstaFotos[count].Link);
                            noNewFoto = true;
                        }
                    }
                }
                _logs.WriteLog("log.txt", "удалили последнюю добавленную т.к. она уже была");
                lastInstaFotos.RemoveAt(lastInstaFotos.Count - 1);
            }

            return lastInstaFotos;
        }

        private WallPost SearchForInstaPost()
        {
            int i = 0;
            WallPost post;
            while (true)
            {
                _logs.WriteLog("log.txt", "GetOwnerWallPosts");
                _logs.WriteLog("log.txt", "i=" + i);
                XmlDocument xmlPost = _myVk.WallGet(1, i);

                i++;
                _logs.WriteLog("log.txt", "GetWallPostFromXML");
                post = GetWallPostFromXML(xmlPost);
                _logs.WriteLog("log.txt", "postID=" + post.ID);

                if (post.Text.Contains("instagr.am") || post.Text.Contains("instagram.com"))
                {
                    break;
                }

                if (post.ID == "")
                {
                    return null;
                }
            }

            return post;
        }

        private WallPost GetWallPostFromXML(XmlDocument xmlPost)
        {
            var post = new WallPost
                           {
                               ID = _network.GetDataFromXmlNode(xmlPost.SelectSingleNode("response/post/id")),
                               Date = _network.GetDataFromXmlNode(xmlPost.SelectSingleNode("response/post/date")),
                               Text = _network.GetDataFromXmlNode(xmlPost.SelectSingleNode("response/post/text")),
                               Comments = _network.GetDataFromXmlNode(xmlPost.SelectSingleNode("response/post/comments/count")),
                               Likes = _network.GetDataFromXmlNode(xmlPost.SelectSingleNode("response/post/likes/count")),
                               Reposts = _network.GetDataFromXmlNode(xmlPost.SelectSingleNode("response/post/reposts/count"))
                           };

            return post;
        }

        private InstaFoto GetInstaFotoFromXML(XmlDocument lastFoto)
        {
            var foto = new InstaFoto
                           {
                               Link = _network.GetDataFromXmlNode(lastFoto.SelectSingleNode("/baseprop/data/link")),
                               LowResolutionUrl = _network.GetDataFromXmlNode(lastFoto.SelectSingleNode("/baseprop/data/images/low_resolution/url")),
                               StandardResolutionUrl = _network.GetDataFromXmlNode(lastFoto.SelectSingleNode("/baseprop/data/images/standard_resolution/url")),
                               Text = _network.GetDataFromXmlNode(lastFoto.SelectSingleNode("/baseprop/data/caption/text")),
                               Tags = _network.GetDataFromXmlNode(lastFoto.SelectSingleNode("/baseprop/data/tags")),
                               Id = _network.GetDataFromXmlNode(lastFoto.SelectSingleNode("/baseprop/data/caption/id")),
                               NextMaxId = _network.GetDataFromXmlNode(lastFoto.SelectSingleNode("/baseprop/pagination/next_max_id"))
                           };
            foto.NextMaxId = foto.NextMaxId.Substring(0, 18);

            SaveFotoToFile(foto);
            return foto;
        }

        private void SaveFotoToFile(InstaFoto foto)
        {
            _logs.WriteLog("log.txt", "SaveFotoToFile");
            _logs.WriteLog("log.txt", "filename");
            _fileName = @"fotos/" + foto.Link.Replace("http://instagram.com/p/", "").Replace("/","") + ".jpg";
            lock (_locker)
            {
                _logs.WriteLog("log.txt", "downloading");
                using (var client = new WebClient())
                {
                    if (!(File.Exists(_fileName)))
                    {
                        _logs.WriteLog("log.txt", "DownloadFile");
                        client.DownloadFile(foto.StandardResolutionUrl, _fileName);
                    }
                }
            }
            foto.Filename = _fileName;
        }
        #endregion

        #region notifyIcon
        private void MenuItemStopClick(object sender, EventArgs e)
        {
            _logs.WriteLog("log.txt", "Aborting thread");
            _thr.Abort();
            _myNotifyIcon.Icon = _iconWait;
        }

        private void MenuItemTryClick(object sender, EventArgs e)
        {
            _logs.WriteLog("log.txt", "Trying once SearchNewFotosAndPostToWallIfExists");
            var tryOnce = new Thread(SearchNewFotosAndPostToWallIfExists) { Name = "Try once thread" };
            tryOnce.Start();
        }

        private void MenuItemExitClick(object sender, EventArgs e)
        {
            Close();
        }

        private void MyNotifyIconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    WindowState = WindowState.Normal;
                    break;
                case WindowState.Normal:
                    WindowState = WindowState.Minimized;
                    break;
            }
        }

        private void MyNotifyIconMouseMove(object sender, MouseEventArgs e)
        {
            var timeToNextUpdate = DateTime.Parse("01:00:00") - (DateTime.Now - _timeOfStartSleeping);
            _myNotifyIcon.Text = String.Format("Time to next upate: {0}:{1}:{2}",
                timeToNextUpdate.Hour,
                timeToNextUpdate.Minute,
                timeToNextUpdate.Second);
        }
        #endregion

        private void WindowStateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    ShowInTaskbar = false;
                    break;
                case WindowState.Normal:
                    ShowInTaskbar = true;
                    break;
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_thr != null)
            {
                _thr.Abort();
            }
            _myNotifyIcon.Visible = false;
            Application.Current.Shutdown();//todo
        }

    }
}
