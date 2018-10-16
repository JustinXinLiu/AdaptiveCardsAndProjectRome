using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Foundation.Collections;
using Windows.System.RemoteSystems;

namespace AdaptiveCardsAndProjectRome.Shared
{
    public delegate void MediaEventHandler(object sender, string cardJson, TimeSpan mediaPlayedPosition);

    public static class RomeShare
    {
        private const string ChannelName = "Media";

        private static RemoteSystemSessionController _sessionController;
        private static RemoteSystemSession _currentSession;
        private static RemoteSystemSessionMessageChannel _mediaChannel;
        private static RemoteSystemSessionWatcher _sessionWatcher;

        public static event EventHandler<string> OnStatusUpdateMessage;
        public static event EventHandler OnSessionListUpdated;

        public static event EventHandler OnRequestMediaData;
        public static event MediaEventHandler OnSetMediaData;

        public static List<RemoteSystemSessionInfo> AvailableSessions = new List<RemoteSystemSessionInfo>();

        public static async void CreateSession()
        {
            var status = await RemoteSystem.RequestAccessAsync();
            if (status == RemoteSystemAccessStatus.Allowed)
            {
                _sessionController = new RemoteSystemSessionController("Connected Experience");
                _sessionController.JoinRequested += SessionController_JoinRequested;

                var result = await _sessionController.CreateSessionAsync();
                DebugString($"Create Session {result.Status}: {result.Session.ControllerDisplayName} {result.Session.DisplayName} {result.Session.Id}");
                _currentSession?.Dispose();
                _currentSession = result.Session;

                _mediaChannel = new RemoteSystemSessionMessageChannel(_currentSession, ChannelName);
                _mediaChannel.ValueSetReceived += OnChannelValueSetReceived;
            }
        }

        public static async void DiscoverSessions()
        {
            var status = await RemoteSystem.RequestAccessAsync();
            if (status == RemoteSystemAccessStatus.Allowed)
            {
                _sessionWatcher?.Stop();
                AvailableSessions.Clear();

                _sessionWatcher = RemoteSystemSession.CreateWatcher();
                _sessionWatcher.Added += SessionWatcher_Added;
                _sessionWatcher.Removed += SessionWatcher_Removed;
                _sessionWatcher.Updated += SessionWatcher_Updated;

                _sessionWatcher.Start();
            }
        }

        public static async void JoinSession(RemoteSystemSessionInfo sessionInfo)
        {
            var info = await sessionInfo.JoinAsync();
            if (info.Status == RemoteSystemSessionJoinStatus.Success)
            {
                _currentSession?.Dispose();
                _currentSession = info.Session;

                _mediaChannel = new RemoteSystemSessionMessageChannel(_currentSession, ChannelName);
                _mediaChannel.ValueSetReceived += OnChannelValueSetReceived;

                await _mediaChannel.BroadcastValueSetAsync(new ValueSet { ["Type"] = "RequestMedia" });
            }
        }

        private static void SessionController_JoinRequested(RemoteSystemSessionController sender, RemoteSystemSessionJoinRequestedEventArgs args)
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
                case "MediaData":
                    var json = args.Message["CardJson"].ToString();
                    var position = TimeSpan.Parse(args.Message["Position"].ToString());
                    OnSetMediaData?.Invoke(null, json, position);
                    break;
                case "RequestMedia":
                    OnRequestMediaData?.Invoke(null, EventArgs.Empty);
                    break;
            }
        }

        private static void SessionWatcher_Removed(RemoteSystemSessionWatcher sender, RemoteSystemSessionRemovedEventArgs args)
        {
            DebugString($"Session Removed {args.SessionInfo.DisplayName}");
            AvailableSessions.Remove(args.SessionInfo);

            OnSessionListUpdated?.Invoke(null, EventArgs.Empty);
        }

        private static void SessionWatcher_Added(RemoteSystemSessionWatcher sender, RemoteSystemSessionAddedEventArgs args)
        {
            DebugString($"Session Added {args.SessionInfo.DisplayName}: {args.SessionInfo.ControllerDisplayName}");
            AvailableSessions.Add(args.SessionInfo);

            OnSessionListUpdated?.Invoke(null, EventArgs.Empty);
        }

        private static void SessionWatcher_Updated(RemoteSystemSessionWatcher sender, RemoteSystemSessionUpdatedEventArgs args)
        {
            DebugString($"Session Updated {args.SessionInfo.DisplayName}");
            AvailableSessions.RemoveAll(i => i.ControllerDisplayName == args.SessionInfo.ControllerDisplayName);
            AvailableSessions.Add(args.SessionInfo);

            OnSessionListUpdated?.Invoke(null, EventArgs.Empty);
        }

        private static void DebugString(string v)
        {
            Debug.WriteLine(v);
            OnStatusUpdateMessage?.Invoke(null, v);

        }

        public static async void SendData(string cardJson, TimeSpan mediaPlayedPosition)
        {
            if (_mediaChannel != null)
            {
                var valueSet = new ValueSet
                {
                    ["Type"] = "MediaData",
                    ["CardJson"] = cardJson,
                    ["Position"] = mediaPlayedPosition
                };

                await _mediaChannel.BroadcastValueSetAsync(valueSet);
            }
        }
    }
}
