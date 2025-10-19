using Microsoft.Maui.Controls.Shapes;
using Newtonsoft.Json;
using System.Net.Http;
using System.Linq;

namespace PlaylistAudiometrics;

public partial class PAinfo : ContentPage
{
    public class TrackInfo
    {
        public string TrackName { get; set; }
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string ImageUrl { get; set; }
    }

    public class CommentInfo
    {
        public string UserName { get; set; }
        public string ProfilFoto { get; set; }
        public int Rating { get; set; }
        public string ReviewText { get; set; }
        public string ReviewDate { get; set; }
        public int Likes { get; set; }
    }

    public class SongInfoResponse
    {
        [JsonProperty("InfoText")]
        public string InfoText { get; set; }
    }

    private static List<TrackInfo> savedTracks = new List<TrackInfo>();
    private List<CommentInfo> comments = new List<CommentInfo>();
    private TrackInfo currentTrack;

    public PAinfo()
    {
        InitializeComponent();

        pickerOrderBy.SelectedIndex = 0;

        string savedJson = Preferences.Get("AllTracks", string.Empty);
        if (!string.IsNullOrEmpty(savedJson))
        {
            savedTracks = JsonConvert.DeserializeObject<List<TrackInfo>>(savedJson);
            cvTracks.ItemsSource = savedTracks;

            lblPlaylistName.Text = Preferences.Get("PlaylistBaslik", "Playlist Name");
            lblPlaylistDescription.Text = Preferences.Get("PlaylistAciklama", "Playlist Description");
        }
    }

    private void OnTrackTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is TrackInfo trackInfo)
        {
            ShowTrackDetails(trackInfo);
        }
    }

    private async void ShowTrackDetails(TrackInfo track)
    {
        currentTrack = track;

        trackDetailsOverlay.IsVisible = true;

        lblDetailsTrackName.Text = track.TrackName;
        lblDetailsArtistName.Text = track.ArtistName;
        lblDetailsAlbumName.Text = track.AlbumName;
        imgDetailsCover.Source = track.ImageUrl;

        await LoadSongDescription(track);
        await LoadCommentsAsync(track);
    }

    private async Task LoadSongDescription(TrackInfo track)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                string url = $"https://backendexpress.com/Handlers/GetSongInfo.ashx" +
                             $"?PlaylistCode={Preferences.Get("PlaylistCode", "0")}" +
                             $"&SongName={Uri.EscapeDataString(track.TrackName)}" +
                             $"&SongAlbum={Uri.EscapeDataString(track.AlbumName)}" +
                             $"&SongArtist={Uri.EscapeDataString(track.ArtistName)}";

                var response = await client.GetStringAsync(url);

                if (!string.IsNullOrEmpty(response))
                {
                    var songInfos = JsonConvert.DeserializeObject<List<SongInfoResponse>>(response);

                    lblSongDescription.Text = (songInfos != null && songInfos.Count > 0)
                        ? songInfos[0].InfoText
                        : "No description available for this track.";
                }
            }
        }
        catch (Exception ex)
        {
            lblSongDescription.Text = "Error loading description: " + ex.Message;
        }
    }

    private async Task LoadCommentsAsync(TrackInfo track)
    {
        try
        {
            commentsContainer.Clear();
            //lblStatus.Text = "Loading comments...";

            using (HttpClient client = new HttpClient())
            {
                string orderBy = (pickerOrderBy.SelectedIndex == 0) ? "BegeniSayisi" : "Tarih";

                string url = $"https://backendexpress.com/Handlers/GetSongComments.ashx" +
                             $"?songTitle={Uri.EscapeDataString(track.TrackName)}" +
                             $"&songArtist={Uri.EscapeDataString(track.ArtistName)}" +
                             $"&order={Uri.EscapeDataString(orderBy)}";

                var json = await client.GetStringAsync(url);

                if (!string.IsNullOrEmpty(json))
                {
                    var reviews = JsonConvert.DeserializeObject<List<CommentInfo>>(json);

                    if (reviews != null && reviews.Count > 0)
                    {
                        comments = reviews;

                        var sortedComments = (orderBy == "BegeniSayisi")
                            ? comments.OrderByDescending(c => c.Likes).ToList()
                            : comments.OrderByDescending(c => DateTime.Parse(c.ReviewDate)).ToList();

                        foreach (var comment in sortedComments)
                        {
                            var commentCard = CreateCommentCard(comment);
                            commentsContainer.Add(commentCard);
                        }

                        //lblStatus.Text = $"{reviews.Count} comment(s) loaded.";
                    }
                    else
                    {
                        //lblStatus.Text = "No comments found.";
                    }
                }
                else
                {
                    //lblStatus.Text = "No data received from server.";
                }
            }
        }
        catch (Exception ex)
        {
            //lblStatus.Text = "Error loading comments: " + ex.Message;
        }
    }

    private Frame CreateCommentCard(CommentInfo comment)
    {
        var frame = new Frame
        {
            CornerRadius = 12,
            Padding = 15,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Color.FromArgb("#e0e0e0"),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 50 },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };

        var profileImage = new Image
        {
            Source = comment.ProfilFoto,
            WidthRequest = 50,
            HeightRequest = 50,
            Aspect = Aspect.AspectFill,
            VerticalOptions = LayoutOptions.Start,
            Clip = new EllipseGeometry { Center = new Point(25, 25), RadiusX = 25, RadiusY = 25 }
        };
        Grid.SetColumn(profileImage, 0);

        var contentStack = new VerticalStackLayout { Spacing = 8 };
        Grid.SetColumn(contentStack, 1);

        var headerStack = new HorizontalStackLayout { Spacing = 10 };

        var usernameLabel = new Label
        {
            Text = comment.UserName,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        };

        var starsLabel = new Label
        {
            Text = GenerateStars(comment.Rating),
            FontSize = 14,
            TextColor = Color.FromArgb("#FFB800")
        };

        var dateLabel = new Label
        {
            Text = comment.ReviewDate,
            FontSize = 12,
            TextColor = Color.FromArgb("#999"),
            HorizontalOptions = LayoutOptions.EndAndExpand
        };

        headerStack.Add(usernameLabel);
        headerStack.Add(starsLabel);
        headerStack.Add(dateLabel);

        var reviewLabel = new Label
        {
            Text = comment.ReviewText,
            FontSize = 14,
            TextColor = Color.FromArgb("#444"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var likesLabel = new Label
        {
            Text = $"❤️ {comment.Likes} likes",
            FontSize = 13,
            TextColor = Color.FromArgb("#e74c3c"),
            FontAttributes = FontAttributes.Bold
        };

        contentStack.Add(headerStack);
        contentStack.Add(reviewLabel);
        contentStack.Add(likesLabel);

        grid.Add(profileImage);
        grid.Add(contentStack);
        frame.Content = grid;

        return frame;
    }

    private string GenerateStars(int rating)
    {
        string stars = "";
        for (int i = 0; i < rating; i++) stars += "⭐";
        for (int i = rating; i < 5; i++) stars += "☆";
        return stars;
    }

    private void OnCloseDetailsTapped(object sender, EventArgs e)
    {
        trackDetailsOverlay.IsVisible = false;
    }

    private async void OnOrderByChanged(object sender, EventArgs e)
    {
        if (currentTrack != null)
        {
            await LoadCommentsAsync(currentTrack);
        }
    }
}
