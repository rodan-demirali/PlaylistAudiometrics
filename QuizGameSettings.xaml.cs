using Microsoft.Maui.Controls;
using Newtonsoft.Json;
using System;

namespace PlaylistAudiometrics;

public partial class QuizGameSettings : ContentPage
{
    public class TrackInfo
    {
        public string TrackName { get; set; }
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string ImageUrl { get; set; }
    }

    private static List<TrackInfo> savedTracks = new List<TrackInfo>();

    public QuizGameSettings()
    {
        InitializeComponent();

        if (QuestionCountPicker.SelectedIndex == -1) QuestionCountPicker.SelectedIndex = 0;
        if (GuessTimePicker.SelectedIndex == -1) GuessTimePicker.SelectedIndex = 0;
        if (OptionCountPicker.SelectedIndex == -1) OptionCountPicker.SelectedIndex = 1;

        if(Preferences.ContainsKey("SoruSayisi"))
        {
            int iSoruSayisi = Preferences.Get("SoruSayisi", 1);
            int iOyunSuresi = Preferences.Get("OyunSuresi", 20);
            int iSecenekSayisi = Preferences.Get("SecenekSayisi", 4);

            QuestionCountPicker.SelectedItem = iSoruSayisi.ToString();
            GuessTimePicker.SelectedItem = iOyunSuresi.ToString();
            OptionCountPicker.SelectedItem = iSecenekSayisi.ToString();
        }



    }

    private async void StartButton_Pressed(object sender, EventArgs e)
    {
        if (sender is VisualElement ve)
            await ve.ScaleTo(0.96, 100, Easing.SinInOut);
    }

    private async void StartButton_Released(object sender, EventArgs e)
    {
        if (sender is VisualElement ve)
            await ve.ScaleTo(1, 100, Easing.SinInOut);
    }

    private async void OnStartGameClicked(object sender, EventArgs e)
    {
        int questionCount = int.Parse(QuestionCountPicker.SelectedItem?.ToString() ?? "10");
        int guessTime = int.Parse(GuessTimePicker.SelectedItem?.ToString() ?? "10");
        int optionCount = int.Parse(OptionCountPicker.SelectedItem?.ToString() ?? "4");

        string savedJson = Preferences.Get("AllTracks", string.Empty);
        if (!string.IsNullOrEmpty(savedJson))
        {
            savedTracks = JsonConvert.DeserializeObject<List<TrackInfo>>(savedJson);
            if(savedTracks.Count < 10)
            {
                questionCount = savedTracks.Count;
            }
        }

        Preferences.Set("SoruSayisi", questionCount);
        Preferences.Set("OyunSuresi", guessTime);
        Preferences.Set("SecenekSayisi", optionCount);

        await Navigation.PushAsync(new QuizGamePage());

        //await DisplayAlert("Baþlýyor", $"Sorular: {questionCount}\nSüre: {guessTime}s\nSeçenekler: {optionCount}", "Tamam");
    }
}
