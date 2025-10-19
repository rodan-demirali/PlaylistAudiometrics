using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Maui.Storage;

namespace PlaylistAudiometrics
{
    public partial class PAmenu : ContentPage
    {
        public class TrackInfo
        {
            public string TrackName { get; set; }
            public string AlbumName { get; set; }
            public string ArtistName { get; set; }
            public string ImageUrl { get; set; }
        }

        private static List<TrackInfo> lstAllTracks = new List<TrackInfo>();
        private static List<TrackInfo> savedTracks = new List<TrackInfo>();

        public PAmenu()
        {
            InitializeComponent();

            var sPlaylistCode = Preferences.Get("PlaylistCode", "bulunamadi");
            var sOldPlaylistCode = Preferences.Get("OldPlaylistCode", "bulunamadi");

            string savedJson = Preferences.Get("AllTracks", string.Empty);
            if (!string.IsNullOrEmpty(savedJson) && sPlaylistCode == sOldPlaylistCode)
            {
                savedTracks = JsonConvert.DeserializeObject<List<TrackInfo>>(savedJson);
                cvTracks.ItemsSource = savedTracks;

                var sBaslik = Preferences.Get("PlaylistBaslik", "Playlist Name");
                var sAciklama = Preferences.Get("PlaylistAciklama", "Playlist Description");
                lblPlaylistName.Text = sBaslik;
                lblPlaylistDescription.Text = sAciklama;

                Shell.SetTabBarIsVisible(this, true);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var sPlaylistCode = Preferences.Get("PlaylistCode", "bulunamadi");
            var sOldPlaylistCode = Preferences.Get("OldPlaylistCode", "bulunamadi");

            if (sPlaylistCode != "bulunamadi")
            {
                string savedJson = Preferences.Get("AllTracks", string.Empty);
                //Preferences.Set("OldPlaylistCode", "bulunamadi");
                //sOldPlaylistCode = "bulunamadi";

                if (!string.IsNullOrEmpty(savedJson) && sPlaylistCode == sOldPlaylistCode)
                {
                    Shell.SetTabBarIsVisible(this, true);
                }
                else
                {
                    Shell.SetTabBarIsVisible(this, false);

                    Preferences.Set("OldPlaylistCode", sPlaylistCode);
                    await SetTheSongs(sPlaylistCode);
                }
            }
            else
            {
                Shell.SetTabBarIsVisible(this, true);
            }
        }

        private async Task SetTheSongs(string playlistId)
        {
            try
            {
                Shell.SetTabBarIsVisible(this, false);

                loadingOverlay.IsVisible = true;
                aiLoading.IsRunning = true;
                pbLoading.Progress = 0;
                lblProgressPercent.Text = "0%";

                string token = await GetSpotifyToken();

                DateTime lastUpdate = DateTime.MinValue;

                var progress = new Progress<double>(p =>
                {
                    if ((DateTime.Now - lastUpdate).TotalMilliseconds < 100 && p < 0.99)
                        return;

                    lastUpdate = DateTime.Now;
                    pbLoading.Progress = p;
                    lblProgressPercent.Text = $"{(int)(p * 100)}%";
                });

                JObject json = await GetPlaylistData(playlistId, token, progress);

                pbLoading.Progress = 1;
                lblProgressPercent.Text = "100%";

                lblPlaylistName.Text = json["name"]?.ToString() ?? string.Empty;
                lblPlaylistDescription.Text = json["description"]?.ToString() ?? string.Empty;

                Preferences.Set("PlaylistBaslik", lblPlaylistName.Text.ToString());
                Preferences.Set("PlaylistAciklama", lblPlaylistDescription.Text.ToString());


                var trackList = await Task.Run(() =>
                {
                    var list = new List<TrackInfo>();
                    var items = json["tracks"]?["items"] as JArray;

                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var track = item["track"];
                            if (track == null) continue;

                            string trackName = track["name"]?.ToString() ?? string.Empty;
                            string albumName = track["album"]?["name"]?.ToString() ?? string.Empty;
                            string artistName = track["artists"]?[0]?["name"]?.ToString() ?? string.Empty;

                            var images = track["album"]?["images"] as JArray;
                            string imageUrl = images != null && images.Count > 0
                                ? images.Last["url"]?.ToString() ?? string.Empty  
                                : string.Empty;

                            if (string.IsNullOrWhiteSpace(trackName) ||
                                string.IsNullOrWhiteSpace(albumName) ||
                                string.IsNullOrWhiteSpace(artistName) ||
                                string.IsNullOrWhiteSpace(imageUrl))
                            {
                                continue;
                            }

                            list.Add(new TrackInfo
                            {
                                TrackName = trackName,
                                AlbumName = albumName,
                                ArtistName = artistName,
                                ImageUrl = imageUrl
                            });
                        }
                    }

                    return list;
                });

                await Task.Run(() =>
                {
                    string jsonString = JsonConvert.SerializeObject(trackList);
                    Preferences.Set("AllTracks", jsonString);
                });

                lstAllTracks = trackList;
                cvTracks.ItemsSource = trackList;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                aiLoading.IsRunning = false;
                loadingOverlay.IsVisible = false;

                Shell.SetTabBarIsVisible(this, true);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            Shell.SetTabBarIsVisible(this, true);
        }

        private async Task<string> GetSpotifyToken()
        {
            string clientId = "1e6a3fcf9d6f457885e9dc625dfcd26f";
            string clientSecret = "53820ef502694bfd87e17a74f0ead3fb";

            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                var request = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("grant_type", "client_credentials")
                });

                var response = await client.PostAsync("https://accounts.spotify.com/api/token", request);
                string responseString = await response.Content.ReadAsStringAsync();
                JObject tokenJson = JObject.Parse(responseString);
                return tokenJson["access_token"].ToString();
            }
        }

        private async Task<JObject> GetPlaylistData(string playlistId, string token, IProgress<double> progress = null)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string url = "https://api.spotify.com/v1/playlists/" + playlistId;
                var response = await client.GetAsync(url);
                string playlistMeta = await response.Content.ReadAsStringAsync();

                JObject fullJson = JObject.Parse(playlistMeta);
                int totalTracks = fullJson["tracks"]?["total"]?.ToObject<int>() ?? 0;

                JArray allTracks = new JArray();

                var firstItems = fullJson["tracks"]?["items"] as JArray;
                if (firstItems != null)
                {
                    foreach (var item in firstItems)
                        allTracks.Add(item);
                }

                if (progress != null && totalTracks > 0)
                    progress.Report(Math.Min(1.0, allTracks.Count / (double)totalTracks));

                string nextUrl = fullJson["tracks"]?["next"]?.ToString();
                int pageCount = 0;

                while (!string.IsNullOrEmpty(nextUrl))
                {
                    var nextResponse = await client.GetAsync(nextUrl);
                    string nextContent = await nextResponse.Content.ReadAsStringAsync();
                    JObject nextJson = JObject.Parse(nextContent);

                    var items = nextJson["items"] as JArray;
                    if (items != null)
                    {
                        foreach (var item in items)
                            allTracks.Add(item);
                    }

                    pageCount++;

                    if (pageCount % 2 == 0 || string.IsNullOrEmpty(nextJson?["next"]?.ToString()))
                    {
                        if (progress != null && totalTracks > 0)
                            progress.Report(Math.Min(1.0, allTracks.Count / (double)totalTracks));
                    }

                    nextUrl = nextJson?["next"]?.ToString();
                }

                fullJson["tracks"]["items"] = allTracks;

                return fullJson;
            }
        }

        private async void btnReturnHome_Clicked(object sender, EventArgs e)
        {
            //await Navigation.PushAsync(new MainPage());
            //await Navigation.PopAsync();
            Application.Current.MainPage = new NavigationPage(new MainPage());
        }
    }
}