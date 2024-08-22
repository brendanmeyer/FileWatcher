﻿using System.Collections.Concurrent;
using System.ComponentModel;
using System.Timers;
using System.Xml.Serialization;
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

        // // Information about the last change
        // private ChangeInfo? _lastChange;

        // // The write time for the last change
        // private DateTime _lastWriteTime;

        // // The timer used to "reset" the FileSystemWatch object
        // private System.Timers.Timer? _timer;

        // // The background worker that processes the file/folder changes
        // private BackgroundWorker? _worker;

        // // The queue that will contain the changes
        // private ConcurrentQueue<ChangeInfo>? _queue;

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
        public void ProcessChange(ChangeInfo change)
        {
            if (change == null || _queue == null || _worker == null)
            {
                return;
            }

            _queue.Enqueue(change);
            if (!_worker.IsBusy)
            {
                _worker.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Starts the watch.
        /// </summary>
        public bool Start()
        {
            if (_fsWatcher != null || _timer != null)
            {
                String path = Path.GetDirectoryName(change.FullPath) ?? "";
                if (Filters != null && Filters.IsSpecified())
                {
                    // If the file or folder is not a match, then don't take
                    // any further actions
                    if (!Filters.IsMatch(change, path, change.Name, change.FullPath))
                    {
                        return false;
                    }
                }

                if (Exclusions != null && Exclusions.IsSpecified())
                {
                    // If the file or folder is in the exclude list, then don't
                    // take any further actions
                    if (Exclusions.Exclude(change, path, change.Name, change.FullPath))
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
                                    Actions.Run(change.Trigger, Path, change.FullPath);
                                }
                            }
                        }

                        if (Commands != null)
                        {
                            if (Commands.CommandList != null && Commands.CommandList.Count > 0)
                            {
                                Commands.Run(change.Trigger, Path, change.FullPath);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="ChangeInfo"/> object associated with the change.
        /// This method will mark related changes as invalid - such as multiple
        /// changes related to a file copy - to avoid any duplicate work being
        /// done on a file or folder.
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
        /// <returns>
        /// The <see cref="ChangeInfo"/> object of the change, otherwise <c>null</c>.
        /// </returns>
        private ChangeInfo? GetChange(TriggerType trigger, string? name, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            try
            {
                // Ignore folder changes as those would be valid for other
                // change events
                if (Directory.Exists(fullPath) && trigger == TriggerType.Change)
                {
                    return null;
                }

                // The flag indicating the change is a valid change and not one
                // derived from a previous change
                bool isValid = true;

                // The current information on the change
                ChangeInfo change = new(trigger, name, fullPath);

                // The last write time of the file
                DateTime writeTime = default;

                // Verify the file exists before attempting to get the last
                // write time
                if (File.Exists(change.FullPath))
                {
                    writeTime = File.GetLastWriteTime(change.FullPath);
                }

                // Check if the change is related to the same file as the last
                // change that was captured
                if (_lastChange != null && _lastChange.FullPath.Equals(change.FullPath))
                {
                    // If the last change was a copy, then this change is
                    // associated with that change as a copy raises multiple
                    // change events for a file - a copy, and several change
                    // events - so mark this change event as invalid
                    if (_lastChange.Trigger == TriggerType.Create)
                    {
                        isValid = false;
                    }

                    // Check if both the last change was a change, and the
                    // current change is also a change, and the write times
                    // are the same. If all conditions are met, then this indicates
                    // the change being made was associated with another action,
                    // such as a copy, and not an actual change made by the user,
                    // so flag the change as not valid.
                    if ((_lastChange.Trigger == TriggerType.Change && trigger == TriggerType.Change) &&
                        _lastWriteTime.Equals(writeTime))
                    {
                        isValid = false;
                    }
                }

                // Store the last change and write time for this change
                _lastChange = change;
                _lastWriteTime = writeTime;

                // Return the change if it is valid, or null if the change
                // isn't valid
                return isValid ? change : null;
            }
            catch
            {
                return null;
            }
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

        /// <summary>
        /// Called when a file or folder is changed.
        /// </summary>
        /// <param name="sender">
        /// The object calling the method.
        /// </param>
        /// <param name="e">
        /// The event parameters.
        /// </param>
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }


            ChangeInfo? change = GetChange(TriggerType.Change, e.Name, e.FullPath);
            if (change != null)
            {
                ProcessChange(change);
            }

        }

        /// <summary>
        /// Called when a file or folder is created.
        /// </summary>
        /// <param name="sender">
        /// The object calling the method.
        /// </param>
        /// <param name="e">
        /// The event parameters.
        /// </param>
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Created)
            {
                return;
            }

            ChangeInfo? change = GetChange(TriggerType.Create, e.Name, e.FullPath);
            if (change != null)
            {
                ProcessChange(change);
            }
        }

        /// <summary>
        /// Called when a file or folder is deleted.
        /// </summary>
        /// <param name="sender">
        /// The object calling the method.
        /// </param>
        /// <param name="e">
        /// The event parameters.
        /// </param>
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Deleted)
            {
                return;
            }

            ChangeInfo? change = GetChange(TriggerType.Delete, e.Name, e.FullPath);
            if (change != null)
            {
                ProcessChange(change);
            }
        }

        /// <summary>
        /// Called when the timers elapsed time has been reached. The timer is
        /// used because the FileSystemWatcher object tends to stop raising
        /// events after a period of time. After the elapsed time, this method
        /// will disable and then re-enable event raising to sort of "reset" 
        /// the FilesSystemWatcher and prevent it from stop listening to
        /// events.
        /// </summary>
        /// <param name="source">
        /// The timer object.
        /// </param>
        /// <param name="e">
        /// The information associated with the elapsed time.
        /// </param>
        private void OnElapsed(object? source, ElapsedEventArgs e)
        {
            if (_fsWatcher != null)
            {
                _fsWatcher.EnableRaisingEvents = false;
                _fsWatcher.EnableRaisingEvents = true;
            }
        }

        /// <summary>
        /// Called when a file or folder is renamed.
        /// </summary>
        /// <param name="sender">
        /// The object calling the method.
        /// </param>
        /// <param name="e">
        /// The event parameters.
        /// </param>
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Renamed)
            {
                return;
            }

            ChangeInfo? change = GetChange(TriggerType.Rename, e.Name, e.FullPath);
            if (change != null)
            {
                ProcessChange(change);
            }
        }

        /// <summary>
        /// Called when the file system watcher throws an exception.
        /// </summary>
        /// <param name="sender">
        /// The object calling the method.
        /// </param>
        /// <param name="e">
        /// The event parameters.
        /// </param>
        private void OnError(object sender, ErrorEventArgs e)
        {
            if (e.GetException().GetType() == typeof(InternalBufferOverflowException))
            {
                Logger.WriteLine(
                    $"File System Watcher internal buffer overflow.",
                    LogLevel.ERROR);
            }
            else
            {
                Logger.WriteLine(
                    $"An error occurred while watching the file system. Exception: {e.GetException().Message}",
                    LogLevel.ERROR);
            }

            if (_fsWatcher != null)
            {
                NotAccessibleError(_fsWatcher, e);
            }
        }

        /// <summary>
        /// Waits for a specified amount of time before the path to watch
        /// exists. The time value is provided by the <see cref="Timeout"/>
        /// property.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the path exists, otherwise <c>false</c>.
        /// </returns>
        private bool PathExists()
        {
            // The amount of time for the thread to sleep
            const int SLEEP_TIME = 500;

            // Calculate the total number of times the thread will wait based
            // on the timeout value and the SLEEP_TIME
            int waitTime = (Timeout * 1000) / SLEEP_TIME;

            // The number of times the thread has slept
            int i = 0;

            while (!Directory.Exists(Path) && waitTime > i)
            {
                Thread.Sleep(SLEEP_TIME);
                i++;
            }

            return Directory.Exists(Path);
        }
    }
}
