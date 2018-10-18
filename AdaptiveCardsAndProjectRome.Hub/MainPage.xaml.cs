using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using AdaptiveCards.Rendering.Uwp;
using AdaptiveCardsAndProjectRome.Shared;
using Windows.UI.Composition;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Animations.Expressions;
using EF = Microsoft.Toolkit.Uwp.UI.Animations.Expressions.ExpressionFunctions;

namespace AdaptiveCardsAndProjectRome.Hub
{
    public sealed partial class MainPage : Page
    {
        private readonly Compositor _compositor;
        private readonly Visual _cardVisual;
        private float _initialPositionY;
        private readonly CompositionPropertySet _progress;

        public MainPage()
        {
            InitializeComponent();

            _compositor = Window.Current.Compositor;
            _cardVisual = VisualExtensions.GetVisual(Card);
            _cardVisual.RelativeSizeAdjustment = Vector2.One;

            _progress = _compositor.CreatePropertySet();
            _progress.InsertScalar("Progress", 0);

            RomeShare.MediaDataUpdated += OnMediaDataUpdated;
            RomeShare.PositionDataUpdated += OnPositionDataUpdated;

            Loaded += async (s, e) =>
            {
                SetupExpressionAnimationsForCard();

                _initialPositionY = (float)ActualHeight;
                ResetCardVisualPosition();

                await RomeShare.CreateSessionAsync();
            };
        }

        private void SetupExpressionAnimationsForCard()
        {
            var progressExpressionNode = _progress.GetReference().GetScalarProperty("Progress");
            _cardVisual.CenterPoint = new Vector3(Card.RenderSize.ToVector2() / 2, 0.0f);

            // Scale the card visual based on the progress.
            _cardVisual.StartAnimation("Scale", EF.Vector3(1.0f, 1.0f, 1.0f) * EF.Lerp(1.0f, 0.8f, progressExpressionNode));
            // Move the card visual based on the progress.
            var offset = progressExpressionNode * _initialPositionY;
            _cardVisual.StartAnimation("Offset.Y", offset);
        }

        private void OnPositionDataUpdated(object sender, float progress)
        {
            _progress.InsertScalar("Progress", 1.0f - progress);

            //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            //    () => _progress.InsertScalar("Progress", (float)_progress));

            //_cardVisual.Offset = new Vector3(0.0f, _initialPositionY * (1.0f - e), 0.0f);
        }

        private void OnMediaDataUpdated(object sender, MediaDataEventArgs e)
        {
            // If the media content already exists, hide it and we will later remove it.
            if (MediaContainer.Child is UIElement card)
            {
                card.Visibility = Visibility.Collapsed;
                ResetCardVisualPosition();
            }

            RenderAdaptiveCard(e.CardJson, e.MediaPlayedPosition, e.MediaUrl);
        }

        private void RenderAdaptiveCard(string cardJson, TimeSpan mediaPlayedPosition, string mediaUrl)
        {
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
                    ExtraLarge = 24
                }
            };
            renderer.HostConfig = hostConfig;

            // Get card from json.
            var card = renderer.RenderAdaptiveCardFromJsonString(cardJson);

            // Attach the card content to UI.
            if (card.FrameworkElement is FrameworkElement element)
            {
                element.Loaded += (s, e) =>
                {
                    var mediaElement = element.Children().OfType<MediaElement>().Single();

                    // TODO: Why mediaElement.Source is null here?? How do we
                    // auto-play without hacking??
                    // If the following works, we don't have to pass in the mediaUrl
                    // at all! Let me know if you know a better workaround. :)
                    //mediaElement.Position = mediaPlayedPosition;
                    //mediaElement.Play();

                    mediaElement.Source = new Uri(mediaUrl);
                    mediaElement.MediaOpened += (o, a) =>
                    {
                        mediaElement.Position = mediaPlayedPosition;
                        //mediaElement.Play(); // This doesn't work...
                    };

                    // Now the UI is ready.
                    IsHitTestVisible = true;
                };

                MediaContainer.Child = element;
                Card.Visibility = Visibility.Visible;
            }
        }

        private void ResetCardVisualPosition() =>
            _cardVisual.Offset = new Vector3(0.0f, _initialPositionY, 0.0f);
    }
}
