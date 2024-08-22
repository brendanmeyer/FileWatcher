using System.Xml.Serialization;
using TE.FileWatcher.Logging;

namespace TE.FileWatcher.Configuration
{
    /// <summary>
    /// The watches root node in the XML file.
    /// </summary>
    [XmlRoot("watches")]
    public class Watches
    {
        /// <summary>
        /// Gets or sets the logging information.
        /// </summary>
        [XmlElement("logging")]
        public Logging Logging { get; set; } = new Logging();

        /// <summary>
        /// Gets or sets the watch folder list.
        /// </summary>
        [XmlElement("watchFolder")]
        public List<WatchFolder>? WatchFolderList { get; set; }

        /// <summary>
        /// Starts the watches.
        /// </summary>
        public void Start()
        {
            if (WatchFolderList == null || WatchFolderList.Count <= 0)
            {
                Logger.WriteLine("No watch folders were specified.", LogLevel.ERROR);
                return;
            }

            foreach (WatchFolder watchFolder in WatchFolderList)
            {
                try
                {
                    Task.Run(() => watchFolder.Start());
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(ex.Message, LogLevel.ERROR);
                }
            }
        }
    }
}
