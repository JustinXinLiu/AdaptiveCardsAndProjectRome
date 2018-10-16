using System.Linq;
using Windows.Devices.Input;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using AdaptiveCards.Rendering.Uwp;
using AdaptiveCardsAndProjectRome.Shared;

namespace AdaptiveCardsAndProjectRome.Tablet
{
    public sealed partial class MainPage : Page
    {
        private MediaElement _mediaElement;
        private string _cardJson;

        public MainPage()
        {
            InitializeComponent();

            RenderAdaptiveCard();

            RomeShare.DiscoverSessions();
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
                };
                CardContainer.Children.Add(element);
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
                Poster = "https://docs.microsoft.com/en-us/adaptive-cards/content/videoposter.png",
            };
            media.Sources.Add(new AdaptiveMediaSource
            {
                MimeType = "video/mp4",
                Url = "https://adaptivecardsblob.blob.core.windows.net/assets/AdaptiveCardsOverviewVideo.mp4"
            });

            // Add all above to our card.
            card.Body.Add(caption);
            card.Body.Add(media);

            return card.ToJson().ToString();
        }

        private void OnCardContainerHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Touch && e.HoldingState == HoldingState.Started)
            {
                // To create a continuous video playing experience, let's pause it here.
                _mediaElement.Pause();

                // Record the location as we want to resume from here on another device.
                var position = _mediaElement.Position;

                // We don't want to visually move the video player. Instead, we want to create the illusion that
                // a "copy" of it is being dragged down to another device. So here we use RenderTargetBitmap to
                // create such visual.
                var bitmap = new RenderTargetBitmap();
                var cardCopy = bitmap.RenderAsync(CardContainer);

                // This "copy" should be placed at the same position to start with.
                RomeShare.SendData(_cardJson, position);
            }
        }

        private void OnCardContainerTapped(object sender, TappedRoutedEventArgs e)
        {
            RomeShare.JoinSession(RomeShare.AvailableSessions.First());
        }
    }
}
