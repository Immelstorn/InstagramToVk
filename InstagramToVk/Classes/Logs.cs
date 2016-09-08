using System;
using System.IO;

namespace InstagramToVK
{
    class Logs
    {
        private static Logs _instance;
        private static object _syncLock = new object();
        private Logs()
        {
            if (!(File.Exists("log.txt")))
            {
                using (File.Create("log.txt"))
                {
                }
            }

            if (!(File.Exists("errorlog.txt")))
            {
                using (File.Create("errorlog.txt"))
                {
                }
            }
        }

        //синглтон
        public static Logs GetLogsClass()
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    if (_instance == null)
                    {
                        _instance = new Logs();
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Writes the log.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="logstring">The logstring.</param>
        public void WriteLog(string file, string logstring)
        {
            File.AppendAllText(file, string.Format("{0} => {1}\n", DateTime.Now, logstring));
        }
    }
}
