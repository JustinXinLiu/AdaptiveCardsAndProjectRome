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
using System.Diagnostics;

namespace AdaptiveCardsAndProjectRome.Receiver
{
    public sealed partial class MainPage : Page
    {
        private readonly Compositor _compositor;
        private Visual _cardVisual;
        private readonly CompositionPropertySet _progress;

        public MainPage()
        {
            InitializeComponent();

            _compositor = Window.Current.Compositor;
            _progress = _compositor.CreatePropertySet();

            RomeShare.MediaDataUpdated += OnMediaDataUpdated;
            RomeShare.PositionDataUpdated += OnPositionDataUpdated;

            Loaded += async (s, e) => await RomeShare.CreateSessionAsync();
        }

        private void SetupExpressionAnimationsForCard()
        {
            _cardVisual = VisualExtensions.GetVisual(Card);
            _cardVisual.RelativeSizeAdjustment = Vector2.One;
            _cardVisual.CenterPoint = new Vector3(Card.RenderSize.ToVector2() / 2, 0.0f);

            var initialPositionY = (float)ActualHeight;
            _cardVisual.Offset = new Vector3(0.0f, initialPositionY, 0.0f);

            _progress.InsertScalar("Progress", 0);
            var progressExpressionNode = _progress.GetReference().GetScalarProperty("Progress");

            // Scale the card visual based on the progress.
            _cardVisual.StartAnimation("Scale", EF.Vector3(1.0f, 1.0f, 1.0f) * EF.Lerp(0.6f, 1.0f, progressExpressionNode));
            // Fade in the card visual based on the progress.
            _cardVisual.StartAnimation("Opacity", EF.Lerp(0.0f, 1.0f, progressExpressionNode));
            // Move the card visual based on the progress.
            var offset = initialPositionY * (1.0f - progressExpressionNode);
            _cardVisual.StartAnimation("Offset.Y", offset);
        }

        private void OnPositionDataUpdated(object sender, float progress)
        {
            Debug.WriteLine(progress);

            _progress.InsertScalar("Progress", progress);
        }

        private void OnMediaDataUpdated(object sender, MediaDataEventArgs e)
        {
            // If the media content already exists, hide it and we will later remove it.
            if (MediaContainer.Child is UIElement card)
            {
                card.Visibility = Visibility.Collapsed;
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
                    SetupExpressionAnimationsForCard();

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
    }
}
