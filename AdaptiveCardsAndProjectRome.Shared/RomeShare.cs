using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.System.RemoteSystems;

namespace AdaptiveCardsAndProjectRome.Shared
{
    public static class RomeShare
    {
        private const string ChannelName = "Media";

        private static RemoteSystemSessionController _sessionController;
        private static RemoteSystemSession _currentSession;
        private static RemoteSystemSessionMessageChannel _mediaChannel;
        private static RemoteSystemSessionWatcher _sessionWatcher;

        public static event EventHandler<string> StatusMessageUpdated;
        public static event EventHandler SessionListUpdated;

        public static event EventHandler<float> PositionDataUpdated;
        public static event MediaDataEventHandler MediaDataUpdated;

        public static List<RemoteSystemSessionInfo> AvailableSessions = new List<RemoteSystemSessionInfo>();

        public static async Task CreateSessionAsync()
        {
            var status = await RemoteSystem.RequestAccessAsync();
            if (status == RemoteSystemAccessStatus.Allowed)
            {
                _sessionController = new RemoteSystemSessionController("Connected Experience");
                _sessionController.JoinRequested += OnSessionControllerJoinRequested;

                var result = await _sessionController.CreateSessionAsync();

                if (result.Status == RemoteSystemSessionCreationStatus.Success)
                {
                    DebugString($"Create Session {result.Status}: {result.Session.ControllerDisplayName} {result.Session.DisplayName} {result.Session.Id}");
                    _currentSession?.Dispose();
                    _currentSession = result.Session;

                    _mediaChannel = new RemoteSystemSessionMessageChannel(_currentSession, ChannelName);
                    _mediaChannel.ValueSetReceived += OnChannelValueSetReceived;
                }
            }
        }

        public static async Task DiscoverSessionsAsync()
        {
            var status = await RemoteSystem.RequestAccessAsync();
            if (status == RemoteSystemAccessStatus.Allowed)
            {
                _sessionWatcher?.Stop();
                AvailableSessions.Clear();

                _sessionWatcher = RemoteSystemSession.CreateWatcher();
                _sessionWatcher.Added += OnSessionWatcherAdded;
                _sessionWatcher.Removed += OnSessionWatcherRemoved;
                _sessionWatcher.Updated += OnSessionWatcherUpdated;

                _sessionWatcher.Start();
            }
        }

        public static async Task JoinSessionAsync(RemoteSystemSessionInfo sessionInfo)
        {
            var info = await sessionInfo.JoinAsync();
            if (info.Status == RemoteSystemSessionJoinStatus.Success)
            {
                _currentSession?.Dispose();
                _currentSession = info.Session;

                _mediaChannel = new RemoteSystemSessionMessageChannel(_currentSession, ChannelName);
                _mediaChannel.ValueSetReceived += OnChannelValueSetReceived;
            }
        }

        private static void OnSessionControllerJoinRequested(RemoteSystemSessionController sender, RemoteSystemSessionJoinRequestedEventArgs args)
        {
            args.JoinRequest.Accept();

            DebugString($"Join Requested {args.JoinRequest.Participant.RemoteSystem.Id}: {args.JoinRequest.Participant.GetHostNames()[0]}");
        }

        private static void OnChannelValueSetReceived(RemoteSystemSessionMessageChannel sender, RemoteSystemSessionValueSetReceivedEventArgs args)
        {
            DebugString($"Received Message {args.Sender.RemoteSystem.DisplayName}: {args.Sender.GetHostNames()[0]}");
            var messageTypeString = args.Message["Type"] as string;

            switch (messageTypeString)
            {
                case "PositionData":
                    var dragPosition = float.Parse(args.Message["DragPosition"].ToString());
                    PositionDataUpdated?.Invoke(sender, dragPosition);
                    break;
                case "MediaData":
                    var cardJson = args.Message["CardJson"].ToString();
                    var mediaPlayedPosition = TimeSpan.Parse(args.Message["MediaPlayedPosition"].ToString());
                    var mediaUrl = args.Message["MediaUrl"].ToString();
                    MediaDataUpdated?.Invoke(sender, new MediaDataEventArgs(cardJson, mediaPlayedPosition, mediaUrl));
                    break;
            }
        }

        private static void OnSessionWatcherRemoved(RemoteSystemSessionWatcher sender, RemoteSystemSessionRemovedEventArgs args)
        {
            DebugString($"Session Removed {args.SessionInfo.DisplayName}");
            AvailableSessions.Remove(args.SessionInfo);

            SessionListUpdated?.Invoke(null, EventArgs.Empty);
        }

        private static void OnSessionWatcherAdded(RemoteSystemSessionWatcher sender, RemoteSystemSessionAddedEventArgs args)
        {
            DebugString($"Session Added {args.SessionInfo.DisplayName}: {args.SessionInfo.ControllerDisplayName}");
            AvailableSessions.Add(args.SessionInfo);

            SessionListUpdated?.Invoke(null, EventArgs.Empty);
        }

        private static void OnSessionWatcherUpdated(RemoteSystemSessionWatcher sender, RemoteSystemSessionUpdatedEventArgs args)
        {
            DebugString($"Session Updated {args.SessionInfo.DisplayName}");
            AvailableSessions.RemoveAll(i => i.ControllerDisplayName == args.SessionInfo.ControllerDisplayName);
            AvailableSessions.Add(args.SessionInfo);

            SessionListUpdated?.Invoke(null, EventArgs.Empty);
        }

        private static void DebugString(string v)
        {
            Debug.WriteLine(v);
            StatusMessageUpdated?.Invoke(null, v);
        }

        public static async Task SendMediaDataAsync(string cardJson, TimeSpan mediaPlayedPosition, string mediaUrl)
        {
            if (_mediaChannel != null)
            {
                var valueSet = new ValueSet
                {
                    ["Type"] = "MediaData",
                    ["CardJson"] = cardJson,
                    ["MediaPlayedPosition"] = mediaPlayedPosition,
                    ["MediaUrl"] = mediaUrl
                };

                await _mediaChannel.BroadcastValueSetAsync(valueSet);
            }
        }

        public static async Task SendPositionDataAsync(float dragPosition)
        {
            if (_mediaChannel != null)
            {
                var valueSet = new ValueSet
                {
                    ["Type"] = "PositionData",
                    ["DragPosition"] = dragPosition
                };

                await _mediaChannel.BroadcastValueSetAsync(valueSet);
            }
        }
    }

    public class MediaDataEventArgs : EventArgs
    {
        public string CardJson { get; set; }
        public TimeSpan MediaPlayedPosition { get; set; }
        public string MediaUrl { get; set; }

        public MediaDataEventArgs(string cardJson, TimeSpan mediaPlayedPosition, string mediaUrl)
        {
            CardJson = cardJson;
            MediaPlayedPosition = mediaPlayedPosition;
            MediaUrl = mediaUrl;
        }
    }

    public delegate void MediaDataEventHandler(object sender, MediaDataEventArgs e);
}
