namespace PlaylistAudiometrics
{
    public partial class MainPage : ContentPage
    {
        string selectedPlaylistUrl = string.Empty;

        public MainPage()
        {
            InitializeComponent();
            txtBoxPlaylistLink.Text = "";
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
        }


        private async void btnConfirm_Clicked(object sender, EventArgs e)
        {
            string sPlaylistUrl = txtBoxPlaylistLink.Text.Trim();

            //https://spotify.link/0anQdVPjwXb
            //https://open.spotify.com/playlist/1H8s1kgrMa2UlP6DQ4vE1s?si=74d61aec7afe47de
            if (string.IsNullOrEmpty(sPlaylistUrl) || (!sPlaylistUrl.Contains("playlist/") && !sPlaylistUrl.Contains("link/")) )
            {
                await DisplayAlert("Error", "Please enter a valid Spotify playlist link.", "OK");
                return;
            }

            //61GsSsRd6yKAkkyQ7S62fM
            string sPlaylistCode = "bulunamadi";
            if (sPlaylistUrl.Contains("playlist/"))
            {
                //https://open.spotify.com/playlist/1H8s1kgrMa2UlP6DQ4vE1s?si=74d61aec7afe47de
                sPlaylistCode = sPlaylistUrl.Split(new[] { "playlist/" }, StringSplitOptions.None)[1].Split('?')[0];
            }
            if (sPlaylistUrl.Contains("link/"))
            {
                //https://spotify.link/0anQdVPjwXb
                sPlaylistCode = sPlaylistUrl.Split(new[] { "link/" }, StringSplitOptions.None)[1].Split('?')[0];

            }
            Preferences.Set("PlaylistCode", sPlaylistCode);

            //send me to PlaylistAudiometricsPage - PAmenu.xaml
            //await Navigation.PushAsync(new PAmenu());

            var sOldPlaylistCode = Preferences.Get("OldPlaylistCode", "bulunamadi");
            if(sPlaylistCode != sOldPlaylistCode)
            {
                Preferences.Set("AnalyzeContent", "bulunamadi");
            }


            Application.Current.MainPage = new AppShell();
             
            await Shell.Current.GoToAsync("//PAmenu");

        }

        private async void OnPlaylistTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string playlistUrl)
            {
                selectedPlaylistUrl = playlistUrl;

                if (string.IsNullOrEmpty(selectedPlaylistUrl) || (!selectedPlaylistUrl.Contains("playlist/") && !selectedPlaylistUrl.Contains("link/")))
                {
                    await DisplayAlert("Error", "Please enter a valid Spotify playlist link.", "OK");
                    return;
                }

                string sPlaylistCode = "bulunamadi";
                if (selectedPlaylistUrl.Contains("playlist/"))
                {
                    sPlaylistCode = selectedPlaylistUrl.Split(new[] { "playlist/" }, StringSplitOptions.None)[1].Split('?')[0];
                }
                if (selectedPlaylistUrl.Contains("link/"))
                {
                    sPlaylistCode = selectedPlaylistUrl.Split(new[] { "link/" }, StringSplitOptions.None)[1].Split('?')[0];

                }
                Preferences.Set("PlaylistCode", sPlaylistCode);


                var sOldPlaylistCode = Preferences.Get("OldPlaylistCode", "bulunamadi");
                if (sPlaylistCode != sOldPlaylistCode)
                {
                    Preferences.Set("AnalyzeContent", "bulunamadi");
                }


                Application.Current.MainPage = new AppShell();
                await Shell.Current.GoToAsync("//PAmenu");


                //DisplayAlert("Playlist Selected", $"Seçilen playlist:\n{playlistUrl}", "OK");
            }
        }

    }
}
