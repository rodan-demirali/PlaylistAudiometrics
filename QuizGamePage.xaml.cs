using Newtonsoft.Json;
using System.Net;
using System.Threading;
using Plugin.Maui.Audio;

namespace PlaylistAudiometrics;

public partial class QuizGamePage : ContentPage
{
    public class TrackInfo
    {
        public string TrackName { get; set; }
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string ImageUrl { get; set; }
    }

    private static List<TrackInfo> savedTracks = new List<TrackInfo>();

    public int iSoruSayisi = 1;
    public int iOyunSuresi = 20;
    public int iSecenekSayisi = 4;
    public int iRoundCount = 0;
    public string[] sQuestionType = ["Title", "Artist"];
    private Random rnd = new Random();

    private int iDogru = 0;
    private int iYanlis = 0;
    private int iBos = 0;

    public string sCurrentAnswer = "";
    private string sCurrentPreviewUrl = "";
    private Button lastClickedButton = null;

    private TrackInfo previousTrackInfo = null;

    bool isTimerRunning = false;
    CancellationTokenSource countdownToken;
    private CancellationTokenSource audioStopToken;

    private readonly IAudioManager audioManager;
    private IAudioPlayer currentPlayer;

    public QuizGamePage()
    {
        InitializeComponent();

        audioManager = AudioManager.Current;

        iSoruSayisi = Preferences.Get("SoruSayisi", 1);
        iOyunSuresi = Preferences.Get("OyunSuresi", 20);
        iSecenekSayisi = Preferences.Get("SecenekSayisi", 4);

        BtnOption1.IsVisible = false;
        BtnOption2.IsVisible = false;
        BtnOption3.IsVisible = false;
        BtnOption4.IsVisible = false;

        iDogru = 0;
        iYanlis = 0;
        iBos = 0;

        List<Button> optionButtons = new() { BtnOption1, BtnOption2, BtnOption3, BtnOption4 };
        for (int i = 0; i < iSecenekSayisi; i++)
            optionButtons[i].IsVisible = true;

        string savedJson = Preferences.Get("AllTracks", string.Empty);
        if (!string.IsNullOrEmpty(savedJson))
            savedTracks = JsonConvert.DeserializeObject<List<TrackInfo>>(savedJson);

        GameOverScreen.IsVisible = false;
        TrackInfoBox.IsVisible = false;


        lblRound.Text = "🏁 Round " + iRoundCount + " / " + iSoruSayisi;
        lblDurum.Text = "✅ " + iDogru + " | ❌ " + iYanlis + " | ⏭️ " + iBos;

        StartCountdownLoop();
    }

    public void oyunuHazirla()
    {
        List<Button> optionButtons = new() { BtnOption1, BtnOption2, BtnOption3, BtnOption4 };
        foreach (var btn in optionButtons)
        {
            if (btn.IsVisible)
            {
                btn.BackgroundColor = Color.FromArgb("#3E8EDE");
                btn.BorderColor = Color.FromArgb("#134AB0");
                btn.BorderWidth = 1;
                btn.IsEnabled = true;
            }
        }
        lastClickedButton = null;

        iRoundCount++;
        lblRound.Text = "🏁 Round " + iRoundCount + " / " + iSoruSayisi;

        int iRndType = rnd.Next(0, 2);
        string sChosenType = sQuestionType[iRndType];
        lblGuessHeading.Text = "Guess the " + sChosenType;

        int iLen = savedTracks.Count();
        int iRndTrack = rnd.Next(0, iLen);

        previousTrackInfo = savedTracks[iRndTrack];

        string sAnswerWord = sChosenType == "Title"
            ? savedTracks[iRndTrack].TrackName
            : savedTracks[iRndTrack].ArtistName;

        sCurrentAnswer = sAnswerWord;
        sCurrentPreviewUrl = SarkiPreviewBul(savedTracks[iRndTrack].TrackName, savedTracks[iRndTrack].ArtistName);

        List<string> lstOptions = new() { sAnswerWord };
        List<TrackInfo> tempTracks = new(savedTracks);

        for (int i = 0; i < iSecenekSayisi - 1; i++)
        {
            if (tempTracks.Count == 0) break;
            int randIndex = rnd.Next(tempTracks.Count);
            var chosenTrack = tempTracks[randIndex];

            string sOptionWord = sChosenType == "Title"
                ? chosenTrack.TrackName
                : chosenTrack.ArtistName;

            if (lstOptions.Contains(sOptionWord))
            {
                tempTracks.RemoveAt(randIndex);
                i--;
                continue;
            }

            lstOptions.Add(sOptionWord);
            tempTracks.RemoveAt(randIndex);
        }

        lstOptions = lstOptions.OrderBy(x => rnd.Next()).ToList();

        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (i < lstOptions.Count)
                optionButtons[i].Text = lstOptions[i];
        }
    }

    private void ShowPreviousTrackInfo()
    {
        if (previousTrackInfo == null)
        {
            TrackInfoBox.IsVisible = false;
            return;
        }

        lblTrackName.Text = previousTrackInfo.TrackName;
        lblAlbumName.Text = previousTrackInfo.AlbumName;
        lblArtistName.Text = previousTrackInfo.ArtistName;

        if (!string.IsNullOrEmpty(previousTrackInfo.ImageUrl))
        {
            imgTrackCover.Source = previousTrackInfo.ImageUrl;
        }
        else
        {
            imgTrackCover.Source = null;
        }

        TrackInfoBox.IsVisible = true;
    }

    private void HideTrackInfoBox()
    {
        TrackInfoBox.IsVisible = false;
    }

    private string SarkiPreviewBul(string sarkiAdi, string sanatciAdi)
    {
        string apiUrl = $"https://api.deezer.com/search?q=track:\"{Uri.EscapeDataString(sarkiAdi)}\" artist:\"{Uri.EscapeDataString(sanatciAdi)}\"";
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = client.GetStringAsync(apiUrl).Result;
            dynamic json = JsonConvert.DeserializeObject(response);

            if (json?.data != null && json.data.Count > 0)
            {
                string previewUrl = json.data[0].preview;
                return previewUrl ?? "";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Deezer API hatası: {ex.Message}");
        }
        return "";
    }

    private async Task PlayPreviewAsync(string previewUrl)
    {
        if (string.IsNullOrEmpty(previewUrl))
        {
            System.Diagnostics.Debug.WriteLine("Preview URL bulunamadı");
            return;
        }

        try
        {
            StopCurrentAudio();

            string tempFileName = Path.Combine(FileSystem.CacheDirectory, $"preview_{Guid.NewGuid()}.mp3");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var audioData = await httpClient.GetByteArrayAsync(previewUrl);
            await File.WriteAllBytesAsync(tempFileName, audioData);

            var audioStream = File.OpenRead(tempFileName);
            currentPlayer = audioManager.CreatePlayer(audioStream);
            currentPlayer.Play();

            System.Diagnostics.Debug.WriteLine($"Şarkı çalınıyor: {previewUrl}");

            int previewDuration = iOyunSuresi + 5;
            System.Diagnostics.Debug.WriteLine($"Preview {previewDuration} saniye çalacak");

            audioStopToken = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(previewDuration), audioStopToken.Token);

                    if (!audioStopToken.Token.IsCancellationRequested)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (currentPlayer != null)
                            {
                                currentPlayer.Stop();
                                System.Diagnostics.Debug.WriteLine($"Preview {previewDuration} saniye sonunda otomatik durduruldu");
                            }
                        });
                    }
                }
                catch (TaskCanceledException) { }
            });

            currentPlayer.PlaybackEnded += async (s, e) =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (File.Exists(tempFileName))
                            File.Delete(tempFileName);
                    }
                    catch { }
                });
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ses çalma hatası: {ex.Message}");
        }
    }

    private void StopCurrentAudio()
    {
        audioStopToken?.Cancel();
        audioStopToken?.Dispose();
        audioStopToken = null;

        if (currentPlayer != null)
        {
            try
            {
                currentPlayer.Stop();
                currentPlayer.Dispose();
                currentPlayer = null;
                System.Diagnostics.Debug.WriteLine("Şarkı durduruldu");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ses durdurma hatası: {ex.Message}");
            }
        }

        CleanupTempAudioFiles();
    }

    private void CleanupTempAudioFiles()
    {
        try
        {
            var cacheDir = FileSystem.CacheDirectory;
            var tempFiles = Directory.GetFiles(cacheDir, "preview_*.mp3");

            foreach (var file in tempFiles)
            {
                try
                {
                    File.Delete(file);
                    System.Diagnostics.Debug.WriteLine($"Geçici dosya silindi: {file}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Dosya silme hatası: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Temizleme hatası: {ex.Message}");
        }
    }

    private async void StartCountdownLoop()
    {
        if (isTimerRunning)
            return;

        isTimerRunning = true;
        countdownToken = new CancellationTokenSource();

        while (!countdownToken.Token.IsCancellationRequested && iRoundCount < iSoruSayisi)
        {
            if (iRoundCount > 0 && previousTrackInfo != null)
            {
                int prepTime = 5;
                lblTimeLeft.TextColor = Colors.Yellow;

                ShowPreviousTrackInfo();

                while (prepTime >= 0 && !countdownToken.Token.IsCancellationRequested)
                {
                    lblTimeLeft.Text = $"⏱ {prepTime}";
                    await Task.Delay(1000);
                    prepTime--;
                }

                HideTrackInfoBox();
            }
            else
            {
                int prepTime = 5;
                lblTimeLeft.TextColor = Colors.Yellow;
                lblGuessHeading.Text = "🕓 Get Ready!";

                while (prepTime >= 0 && !countdownToken.Token.IsCancellationRequested)
                {
                    lblTimeLeft.Text = $"⏱ {prepTime}";
                    await Task.Delay(1000);
                    prepTime--;
                }
            }

            oyunuHazirla();

            if (iRoundCount > iSoruSayisi)
                break;

            if (!string.IsNullOrEmpty(sCurrentPreviewUrl))
            {
                await PlayPreviewAsync(sCurrentPreviewUrl);
            }

            int gameTime = iOyunSuresi;
            lblTimeLeft.TextColor = Color.FromArgb("#07ed1a");

            while (gameTime >= 0 && !countdownToken.Token.IsCancellationRequested)
            {
                lblTimeLeft.Text = $"⏱ {gameTime}";

                if (gameTime == 5)
                    OnCountdownWarning();
                else if (gameTime == 0)
                    OnCountdownFinished();

                await Task.Delay(1000);
                gameTime--;
            }

            StopCurrentAudio();
        }

        isTimerRunning = false;

        if (iRoundCount >= iSoruSayisi)
        {
            ShowGameOverScreen();
        }
    }

    private void ShowGameOverScreen()
    {
        GameScreen.IsVisible = false;
        GameOverScreen.IsVisible = true;

        lblGameOverCorrect.Text = $"✅ Correct: {iDogru}";
        lblGameOverWrong.Text = $"❌ Wrong: {iYanlis}";
        lblGameOverEmpty.Text = $"⏭️ Skipped: {iBos}";

        int totalQuestions = iDogru + iYanlis + iBos;
        double percentage = totalQuestions > 0 ? (double)iDogru / totalQuestions * 100 : 0;
        lblGameOverScore.Text = $"Score: {percentage:F1}%";
    }

    private void OnCountdownWarning()
    {
        lblTimeLeft.TextColor = Colors.Orange;
    }

    private void OnCountdownFinished()
    {
        lblTimeLeft.TextColor = Colors.Red;

        BtnOption1.IsEnabled = false;
        BtnOption2.IsEnabled = false;
        BtnOption3.IsEnabled = false;
        BtnOption4.IsEnabled = false;

        List<Button> optionButtons = new() { BtnOption1, BtnOption2, BtnOption3, BtnOption4 };
        foreach (var btn in optionButtons)
        {
            if (btn.IsVisible && btn.Text == sCurrentAnswer)
            {
                if (btn == lastClickedButton)
                {
                    iDogru++;
                }

                btn.BackgroundColor = Colors.Green;
                btn.BorderColor = Color.FromArgb("#006400");
                btn.BorderWidth = 3;
            }

            if (btn.IsVisible && btn == lastClickedButton && btn.Text != sCurrentAnswer)
            {
                iYanlis++;
                btn.BackgroundColor = Colors.Red;
                btn.BorderColor = Color.FromArgb("#A11212");
                btn.BorderWidth = 3;
            }
        }
        if (lastClickedButton == null)
        {
            iBos++;
        }

        lblDurum.Text = "✅ " + iDogru + " | ❌ " + iYanlis + " | ⏭️ " + iBos;
    }

    private void BtnOptions_Clicked(object sender, EventArgs e)
    {
        List<Button> optionButtons = new() { BtnOption1, BtnOption2, BtnOption3, BtnOption4 };
        foreach (var btn in optionButtons)
        {
            btn.BackgroundColor = Color.FromArgb("#3E8EDE");
            btn.BorderColor = Color.FromArgb("#134AB0");
            btn.BorderWidth = 1;
        }

        lastClickedButton = (Button)sender;
        lastClickedButton.BackgroundColor = Color.FromArgb("#808FC2");
        lastClickedButton.BorderColor = Color.FromArgb("#4B526E");
        lastClickedButton.BorderWidth = 3;
    }

    private void BtnPlayAgain_Clicked(object sender, EventArgs e)
    {
        countdownToken?.Cancel();
        countdownToken?.Dispose();
        StopCurrentAudio();

        isTimerRunning = false;

        iDogru = 0;
        iYanlis = 0;
        iBos = 0;
        iRoundCount = 0;
        lastClickedButton = null;
        previousTrackInfo = null;

        GameOverScreen.IsVisible = false;
        GameScreen.IsVisible = true;
        TrackInfoBox.IsVisible = false;

        List<Button> optionButtons = new() { BtnOption1, BtnOption2, BtnOption3, BtnOption4 };
        foreach (var btn in optionButtons)
        {
            btn.BackgroundColor = Color.FromArgb("#3E8EDE");
            btn.BorderColor = Color.FromArgb("#134AB0");
            btn.BorderWidth = 1;
        }
        BtnOption1.Text = "Option A";
        BtnOption2.Text = "Option B";
        BtnOption3.Text = "Option C";
        BtnOption4.Text = "Option D";

        lblRound.Text = "🏁 Round " + iRoundCount + " / " + iSoruSayisi;
        lblDurum.Text = "✅ " + iDogru + " | ❌ " + iYanlis + " | ⏭️ " + iBos;
        lblGuessHeading.Text = "🕓 Get Ready!";

        StartCountdownLoop();
    }

    private async void BtnReturnMenu_Clicked(object sender, EventArgs e)
    {
        countdownToken?.Cancel();
        StopCurrentAudio();
        isTimerRunning = false;

        await Shell.Current.GoToAsync("..");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        countdownToken?.Cancel();
        StopCurrentAudio();
        isTimerRunning = false;
    }
}