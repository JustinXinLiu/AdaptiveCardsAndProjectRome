using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.System.RemoteSystems;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using AdaptiveCards.Rendering.Uwp;
using AdaptiveCardsAndProjectRome.Shared;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.Toolkit.Uwp.UI.Animations.Expressions;
using Microsoft.Toolkit.Uwp.UI.Extensions;
//using EF = Microsoft.Toolkit.Uwp.UI.Animations.Expressions.ExpressionFunctions;

namespace AdaptiveCardsAndProjectRome.Sender
{
    public sealed partial class MainPage : Page, IInteractionTrackerOwner
    {
        private MediaElement _mediaElement;
        private string _cardJson;
        private TimeSpan _mediaPlayedPosition;
        private const string PosterUrl =
            "https://docs.microsoft.com/en-us/adaptive-cards/content/videoposter.png";
        private const string MediaUrl =
            "https://adaptivecardsblob.blob.core.windows.net/assets/AdaptiveCardsOverviewVideo.mp4";

        private readonly Compositor _compositor;
        private readonly InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;
        private readonly CompositionPropertySet _progress;
        private Visual _hitTestVisual;
        private Visual _mediaCopyVisual;
        private PointerPoint _pressedPoint;
        private float _maxDistance;

        private bool _idleStateEntered;

        public MainPage()
        {
            InitializeComponent();

            _compositor = Window.Current.Compositor;
            _tracker = InteractionTracker.CreateWithOwner(_compositor, this);
            _progress = _compositor.CreatePropertySet();

            RenderAdaptiveCard();

            RomeShare.SessionListUpdated += OnSessionListUpdated;
            Loaded += async (s, e) => await RomeShare.DiscoverSessionsAsync();

            // When the size of the app changes, we need to update all the measures.
            SizeChanged += (s, e) =>
            {
                if (_hitTestVisual != null)
                {
                    _hitTestVisual.Size = Card.RenderSize.ToVector2();

                    var distanceFromTop = (float)Card.RelativePosition(this).Y;
                    _maxDistance = distanceFromTop + _hitTestVisual.Size.Y;
                    _tracker.MaxPosition = new Vector3(_maxDistance);

                    var trackerNode = _tracker.GetReference();
                    _progress.StartAnimation("Progress", trackerNode.Position.Y / _tracker.MaxPosition.Y);
                }
            };
        }

        private void RenderAdaptiveCard()
        {
            // We want to make sure _mediaElement and _cardJson are populated before any interaction to the UI.
            IsHitTestVisible = false;

            // Create a new adaptive card renderer.
            var renderer = new AdaptiveCardRenderer();

            // Customize the font sizes via AdaptiveHostConfig.
            var hostConfig = new AdaptiveHostConfig
            {
                FontFamily = "Tw Cen MT",
                FontSizes =
                {
                    Small = 14,
                    Default = 16,
                    Medium = 16,
                    Large = 20,
                    ExtraLarge= 24
                }
            };
            renderer.HostConfig = hostConfig;

            // Get card from json.
            _cardJson = CreateMediaAdaptiveCardJson();
            var card = renderer.RenderAdaptiveCardFromJsonString(_cardJson);

            // Monitor when video is played...
            // TODO: Not fired... Bug???
            card.MediaClicked += (c, e) => { };

            // Attach the card content to UI.
            if (card.FrameworkElement is FrameworkElement element)
            {
                element.Loaded += (s, e) =>
                {
                    _mediaElement = element.Children().OfType<MediaElement>().Single();

                    // Now the UI is ready.
                    IsHitTestVisible = true;

                    SetupInteractionTracker();
                };
                MediaContainer.Child = element;
            }
        }

        private string CreateMediaAdaptiveCardJson()
        {
            var card = new AdaptiveCard
            {
                Version = "1.1",
                FallbackText = "This card requires Media to be viewed. Ask your platform to update to Adaptive Cards v1.1 for this an more!"
            };

            // Create caption text.
            var caption = new AdaptiveTextBlock
            {
                Size = TextSize.ExtraLarge,
                Weight = TextWeight.Bolder,
                Text = "Publish Adaptive Card schema"
            };

            // Create video media.
            var media = new AdaptiveMedia
            {
                Poster = PosterUrl
            };
            media.Sources.Add(new AdaptiveMediaSource
            {
                MimeType = "video/mp4",
                Url = MediaUrl
            });

            // Add all above to our card.
            card.Body.Add(caption);
            card.Body.Add(media);

            return card.ToJson().ToString();
        }

        private async void OnCardChromeHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Touch && e.HoldingState == HoldingState.Started)
            {
                // To create a continuous video playing experience, let's pause it here.
                _mediaElement.Pause();

                // Record the location as we want to resume from here on another device.
                _mediaPlayedPosition = _mediaElement.Position;

                // We don't want to visually move the video player. Instead, we want to create the illusion that
                // a "copy" of it is being dragged down to another device. So here we use RenderTargetBitmap to
                // create such visual.
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(Card);
                MediaCopy.Source = bitmap;

                MediaContainer.IsHitTestVisible = false;

                // Create animations to show that a "copy" of the video player is popped up and ready to be dragged up.
                Card.Fade(0.3f).Start();
                MediaCopy
                    .Fade(0.7f, 1)
                    .Then()
                    .Scale(0.975f, 0.975f, (float)Card.ActualWidth / 2, (float)Card.ActualHeight / 2, 300d)
                    .Then()
                    .Offset(offsetY: -24.0f, duration: 400d)
                    .Start();

                // Create an animation that changes the offset of the "copy" based on the manipulation progress.
                _mediaCopyVisual = VisualExtensions.GetVisual(MediaCopy);
                var progressExpressionNode = _progress.GetReference().GetScalarProperty("Progress");
                _mediaCopyVisual.StartAnimation("Offset.Y", progressExpressionNode * -_maxDistance);

                try
                {
                    // Let InteractionTracker to handle the swipe gesture.
                    _interactionSource.TryRedirectForManipulation(_pressedPoint);

                    // Send the card json and media played position over using Remote Sessions API.
                    await RomeShare.SendMediaDataAsync(_cardJson, _mediaPlayedPosition, MediaUrl);
                }
                catch (UnauthorizedAccessException) { }
            }
        }

        private void OnCardChromePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // We need to store the pressed point 'cause Holding event doesn't tell us that.
            _pressedPoint = e.GetCurrentPoint(sender as UIElement);
        }

        private void SetupInteractionTracker()
        {
            // Setup the HitTest visual of the InteractionTracker.
            _hitTestVisual = VisualExtensions.GetVisual(Card);
            _hitTestVisual.Size = Card.RenderSize.ToVector2();
            // TODO: Why this doesn't work?
            //_hitTestVisual.RelativeSizeAdjustment = Vector2.One;

            // In this sample, we only want interactions happening on the Y-axis.
            _interactionSource = VisualInteractionSource.Create(_hitTestVisual);
            _interactionSource.PositionYSourceMode = InteractionSourceMode.EnabledWithInertia;
            _tracker.InteractionSources.Add(_interactionSource);

            // Setup max position of the InteractionTracker.
            var distanceFromTop = (float)Card.RelativePosition(this).Y;
            _maxDistance = distanceFromTop + _hitTestVisual.Size.Y;
            _tracker.MaxPosition = new Vector3(_maxDistance);

            // Initialize the manipulation progress.
            // Note: In this simple demo, we could have used the trackerNode.Position.Y to manipulate the offset
            // directly. But we use the manipulation progress here so we could control more things such as the
            // scale or opacity of the "copy" in the future.
            _progress.InsertScalar("Progress", 0);

            // Create an animation that tracks the progress of the manipulation and stores it in a the PropertySet _progress.
            var trackerNode = _tracker.GetReference();
            // Note here we don't want to EF.Clamp the value 'cause we want the overpan which gives a more natural feel
            // when you pan it.
            _progress.StartAnimation("Progress", trackerNode.Position.Y / _tracker.MaxPosition.Y);

            ConfigureRestingPoints();

            void ConfigureRestingPoints()
            {
                // Setup a possible inertia endpoint (snap point) for the InteractionTracker's minimum position.
                var endpoint1 = InteractionTrackerInertiaRestingValue.Create(_compositor);

                // Use this endpoint when the natural resting position of the interaction is less than the halfway point.
                var trackerTarget = ExpressionValues.Target.CreateInteractionTrackerTarget();
                endpoint1.SetCondition(trackerTarget.NaturalRestingPosition.Y < (trackerTarget.MaxPosition.Y - trackerTarget.MinPosition.Y) / 2);

                // Set the result for this condition to make the InteractionTracker's y position the minimum y position.
                endpoint1.SetRestingValue(trackerTarget.MinPosition.Y);

                // Setup a possible inertia endpoint (snap point) for the InteractionTracker's maximum position.
                var endpoint2 = InteractionTrackerInertiaRestingValue.Create(_compositor);

                // Use this endpoint when the natural resting position of the interaction is more than the halfway point.
                endpoint2.SetCondition(trackerTarget.NaturalRestingPosition.Y >= (trackerTarget.MaxPosition.Y - trackerTarget.MinPosition.Y) / 2);

                // Set the result for this condition to make the InteractionTracker's y position the maximum y position.
                endpoint2.SetRestingValue(trackerTarget.MaxPosition.Y);

                _tracker.ConfigurePositionYInertiaModifiers(new InteractionTrackerInertiaModifier[] { endpoint1, endpoint2 });
            }
        }

        private async Task ResetMediaCopyAsync()
        {
            CardsHost.IsHitTestVisible = false;

            // Reset the opacity, scale and position of the "copy" and fade in the real video player.
            await MediaCopy
                .Offset(offsetY: 0.0f, duration: 300d)
                .Scale(1.0f, 1.0f, (float)Card.ActualWidth / 2, (float)Card.ActualHeight / 2, 300d)
                .StartAsync();

            MediaCopy.Fade(0.0f, 400d).Start();
            await Card.Fade(1.0f, 400d).StartAsync();

            // Reset the InteractionTracker's position.
            _tracker.TryUpdatePosition(Vector3.Zero);

            CardsHost.IsHitTestVisible = true;
            MediaContainer.IsHitTestVisible = true;
        }

        private async void OnSessionListUpdated(object sender, EventArgs e)
        {
            if (RomeShare.AvailableSessions.FirstOrDefault() is RemoteSystemSessionInfo session)
            {
                await RomeShare.JoinSessionAsync(session);

                await DispatcherHelper.ExecuteOnUIThreadAsync(() => ConnectedText.Visibility = Visibility.Visible);
            }
            else
            {
                await DispatcherHelper.ExecuteOnUIThreadAsync(() => ConnectedText.Visibility = Visibility.Collapsed);
            }
        }

        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {
            Debug.WriteLine(nameof(CustomAnimationStateEntered));
        }

        public async void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            Debug.WriteLine(nameof(IdleStateEntered));

            _idleStateEntered = true;
            await ResetMediaCopyAsync();
        }

        public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            Debug.WriteLine(nameof(InertiaStateEntered));
        }

        public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            Debug.WriteLine(nameof(InteractingStateEntered));
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {
            Debug.WriteLine(nameof(RequestIgnored));
        }

        public async void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            var dragPercent = args.Position.Y / _maxDistance;

            if (_idleStateEntered)
            {
                _idleStateEntered = false;
            }
            else
            {
                Debug.WriteLine(dragPercent);
                await RomeShare.SendPositionDataAsync(dragPercent);
            }
        }
    }
}
