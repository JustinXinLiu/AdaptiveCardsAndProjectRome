using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using AdaptiveCards.Rendering.Uwp;
using AdaptiveCardsAndProjectRome.Shared;

namespace AdaptiveCardsAndProjectRome.Hub
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            RomeShare.MediaDataUpdated += OnMediaDataUpdated;
            RomeShare.PositionDataUpdated += OnPositionDataUpdated;
            Loaded += async (s, e) => await RomeShare.CreateSessionAsync();
        }

        private void OnPositionDataUpdated(object sender, float e)
        {
        }

        private void OnMediaDataUpdated(object sender, MediaDataEventArgs e)
        {
            RenderAdaptiveCard(e.CardJson, e.MediaPlayedPosition);
        }

        private void RenderAdaptiveCard(string cardJson, TimeSpan mediaPlayedPosition)
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
                    ExtraLarge= 24
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
                    // mediaElement.Source is null here.

                    //mediaElement.MediaOpened += (o, a) =>
                    //{
                    //    mediaElement.Position = mediaPlayedPosition;
                    //    mediaElement.Play();
                    //};
                    mediaElement.Position = mediaPlayedPosition;
                    mediaElement.Play();

                    // Now the UI is ready.
                    IsHitTestVisible = true;
                };
                CardContainer.Children.Add(element);
                CardContainer.Visibility = Visibility.Visible;
            }
        }
    }
}
