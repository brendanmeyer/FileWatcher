using System.Collections.Concurrent;
using System.ComponentModel;
using System.Timers;
using System.Xml.Serialization;
using TE.FileWatcher.Configuration.Actions;
using TE.FileWatcher.Logging;

namespace TE.FileWatcher.Configuration
{
    /// <summary>
    /// The watch element in the XML file.
    /// </summary>
    public class Watch
    {
        // The file system watcher object
        private FileSystemWatcher? _fsWatcher;

        // Information about the last change
        private ChangeInfo? _lastChange;

        // The write time for the last change
        private DateTime _lastWriteTime;

        // The timer used to "reset" the FileSystemWatch object
        private System.Timers.Timer? _timer;

        // The background worker that processes the file/folder changes
        private BackgroundWorker? _worker;

        // The queue that will contain the changes
        private ConcurrentQueue<ChangeInfo>? _queue;

        /// <summary>
        /// Gets or sets the filters.
        /// </summary>
        [XmlElement("filters")]
        public Filters.Filters? Filters { get; set; }

        /// <summary>
        /// Gets or sets the exclusions.
        /// </summary>
        [XmlElement("exclusions")]
        public Exclusions.Exclusions? Exclusions { get; set; }

        /// <summary>
        /// Gets or sets the notifications for the watch.
        /// </summary>
        [XmlElement("notifications")]
        public Notifications.Notifications? Notifications { get; set; }

        /// <summary>
        /// Gets or sets the actions for the watch.
        /// </summary>
        [XmlElement("actions")]
        public Actions.Actions? Actions { get; set; }

        /// <summary>
        /// Gets or sets the commands for the watch.
        /// </summary>
        [XmlElement("commands")]
        public Commands.Commands? Commands { get; set; }

        /// <summary>
        /// Gets the flag indicating the watch is running.
        /// </summary>
        [XmlIgnore]
        public bool IsRunning
        {
            get
            {
                return (_fsWatcher != null && _fsWatcher.EnableRaisingEvents);
            }
        }

        /// <summary>
        /// Processes the file or folder change.
        /// </summary>
        /// <param name="trigger">
        /// The type of change.
        /// </param>
        /// <param name="name">
        /// The name of the file or folder.
        /// </param>
        /// <param name="fullPath">
        /// The full path of the file or folder.
        /// </param>
        public bool ProcessChange(ChangeInfo change)
        {
            if (change != null)
            {
                String path = Path.GetDirectoryName(change.FullPath) ?? "";
                if (Filters != null && Filters.IsSpecified())
                {
                    // If the file or folder is not a match, then don't take
                    // any further actions
                    if (!Filters.IsMatch(path, change.Name, change.FullPath))
                    {
                        return false;
                    }
                }

                if (Exclusions != null && Exclusions.IsSpecified())
                {
                    // If the file or folder is in the exclude list, then don't
                    // take any further actions
                    if (Exclusions.Exclude(path, change.Name, change.FullPath))
                    {
                        return false;
                    }
                }

                if (Notifications != null)
                {
                    if (Notifications.NotificationList != null && Notifications.NotificationList.Count > 0)
                    {
                        // Send the notifications
                        string? messageType = GetMessageTypeString(change.Trigger);
                        if (!string.IsNullOrWhiteSpace(messageType))
                        {
                            Notifications.Send(change.Trigger, $"{messageType}: {change.FullPath}");
                        }
                    }
                }

                if (Actions != null)
                {
                    if (Actions.ActionList != null && Actions.ActionList.Count > 0)
                    {
                        // Only run the actions if a file wasn't deleted, as the file no
                        // longer exists so no action can be taken on the file
                        if (change.Trigger != TriggerType.Delete)
                        {
                            Actions.Run(change.Trigger, path, change.FullPath);
                        }
                    }
                }

                if (Commands != null)
                {
                    if (Commands.CommandList != null && Commands.CommandList.Count > 0)
                    {
                        Commands.Run(change.Trigger, path, change.FullPath);
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the string value for the message type.
        /// </summary>
        /// <param name="trigger">
        /// The notification trigger.
        /// </param>
        /// <returns>
        /// The string value for the message type, otherwise <c>null</c>.
        /// </returns>
        private static string? GetMessageTypeString(TriggerType trigger)
        {
            string? messageType = null;
            switch (trigger)
            {
                case TriggerType.Create:
                    messageType = "Created";
                    break;
                case TriggerType.Change:
                    messageType = "Changed";
                    break;
                case TriggerType.Delete:
                    messageType = "Deleted";
                    break;
                case TriggerType.Rename:
                    messageType = "Renamed";
                    break;
            }

            return messageType;
        }

        /// <summary>
        /// Reset the FileSystemWatcher object by disabling and attempting to
        /// re-enable the event listening for the object.
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="e">
        /// The event arguments related to the exception.
        /// </param>
        private static void NotAccessibleError(FileSystemWatcher source, ErrorEventArgs e)
        {
            source.EnableRaisingEvents = false;
            int iMaxAttempts = 120;
            int iTimeOut = 30000;
            int i = 0;
            while (source.EnableRaisingEvents == false && i < iMaxAttempts)
            {
                i += 1;
                try
                {
                    source.EnableRaisingEvents = true;
                }
                catch
                {
                    source.EnableRaisingEvents = false;
                    Thread.Sleep(iTimeOut);
                }
            }

        }
    }
}
