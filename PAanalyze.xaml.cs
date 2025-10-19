using Microsoft.Maui.Graphics;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace PlaylistAudiometrics;

public partial class PAanalyze : ContentPage
{
    public class TrackInfo
    {
        public string TrackName { get; set; }
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string ImageUrl { get; set; }
    }

    private static List<TrackInfo> savedTracks = new List<TrackInfo>();
    private const int DAILY_LIMIT = 3;

    public PAanalyze()
    {
        InitializeComponent();
        //Preferences.Set("RemainingAnalyses", 3);

        string savedJson = Preferences.Get("AllTracks", string.Empty);

        if (!string.IsNullOrEmpty(savedJson))
        {
            savedTracks = JsonConvert.DeserializeObject<List<TrackInfo>>(savedJson);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        resultsFrame.IsVisible = false;
        loadingLayout.IsVisible = false;

        CheckAndUpdateDailyLimit();

        var sPlaylistCode = Preferences.Get("PlaylistCode", "bulunamadi");
        var sOldPlaylistCode = Preferences.Get("OldPlaylistCode", "bulunamadi");
        var sAnalyzeContent = Preferences.Get("AnalyzeContent", "bulunamadi");

        if (sAnalyzeContent != "bulunamadi" && sPlaylistCode != "bulunamadi" && sPlaylistCode == sOldPlaylistCode)
        {
            lblAnalyzeResult.Text = sAnalyzeContent;
            resultsFrame.IsVisible = true;
        }
    }

    private void CheckAndUpdateDailyLimit()
    {
        string lastResetDateStr = Preferences.Get("LastResetDate", string.Empty);
        int remainingAnalyses = Preferences.Get("RemainingAnalyses", DAILY_LIMIT);

        DateTime today = DateTime.Now.Date;

        if (string.IsNullOrEmpty(lastResetDateStr))
        {
            Preferences.Set("LastResetDate", today.ToString("yyyy-MM-dd"));
            Preferences.Set("RemainingAnalyses", DAILY_LIMIT);
            remainingAnalyses = DAILY_LIMIT;
        }
        else
        {
            DateTime lastResetDate = DateTime.Parse(lastResetDateStr);

            if (today > lastResetDate)
            {
                Preferences.Set("LastResetDate", today.ToString("yyyy-MM-dd"));
                Preferences.Set("RemainingAnalyses", DAILY_LIMIT);
                remainingAnalyses = DAILY_LIMIT;
            }
        }

        UpdateLimitLabel(remainingAnalyses);

        if (remainingAnalyses <= 0)
        {
            btnAnalyzeGemini.IsEnabled = false;
            btnAnalyzeGemini.Text = "❌ Daily Limit Reached";
        }
        else
        {
            btnAnalyzeGemini.IsEnabled = true;
            btnAnalyzeGemini.Text = "Analyze with Gemini";
        }
    }

    private void UpdateLimitLabel(int remaining)
    {
        lblDailyLimit.Text = $"Daily analyses remaining: {remaining}/{DAILY_LIMIT}";

        // Progress bar güncelleme
        progressBar.Progress = (double)remaining / DAILY_LIMIT;

        if (remaining <= 0)
        {
            lblDailyLimit.TextColor = Color.FromArgb("#ff6b6b");
            progressBar.ProgressColor = Color.FromArgb("#ff6b6b");

            DateTime tomorrow = DateTime.Now.Date.AddDays(1);
            TimeSpan timeUntilReset = tomorrow - DateTime.Now;
            lblResetTime.Text = $"⏰ Resets in: {timeUntilReset.Hours}h {timeUntilReset.Minutes}m";
            lblResetTime.IsVisible = true;
        }
        else if (remaining == 1)
        {
            lblDailyLimit.TextColor = Color.FromArgb("#ffa500");
            progressBar.ProgressColor = Color.FromArgb("#ffa500");
            lblResetTime.IsVisible = false;
        }
        else
        {
            lblDailyLimit.TextColor = Color.FromArgb("#b3b3b3");
            progressBar.ProgressColor = Color.FromArgb("#1DB954");
            lblResetTime.IsVisible = false;
        }
    }

    private string GeminiAPICagir(string prompt)
    {
        string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            return "❌ Error: Gemini API key not found. Please set GEMINI_API_KEY environment variable.";
        }

        string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

        var requestBody = new
        {
            contents = new[]
            {
            new
            {
                parts = new[]
                {
                    new { text = prompt }
                }
            }
        }
        };

        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Headers.Add("X-goog-api-key", apiKey);

            string json = JsonConvert.SerializeObject(requestBody);
            byte[] data = Encoding.UTF8.GetBytes(json);

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            using (WebResponse response = request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                string jsonResponse = reader.ReadToEnd();
                dynamic parsed = JsonConvert.DeserializeObject(jsonResponse);
                string yanit = parsed.candidates[0].content.parts[0].text;
                return yanit;
            }
        }
        catch (Exception ex)
        {
            return "❌ Error: " + ex.Message;
        }
    }


    private async void btnAnalyzeGemini_Clicked(object sender, EventArgs e)
    {
        int remainingAnalyses = Preferences.Get("RemainingAnalyses", DAILY_LIMIT);

        if (remainingAnalyses <= 0)
        {
            await DisplayAlert("⏰ Daily Limit Reached",
                "You have used all your daily analyses. Please try again tomorrow.",
                "OK");
            return;
        }

        var sPlaylistCode = Preferences.Get("PlaylistCode", "bulunamadi");
        var sOldPlaylistCode = Preferences.Get("OldPlaylistCode", "bulunamadi");

        if (sPlaylistCode != sOldPlaylistCode)
        {
            return;
        }

        btnAnalyzeGemini.IsEnabled = false;
        resultsFrame.IsVisible = false;
        loadingLayout.IsVisible = true;

        string playlistContent = "";
        int counter = 1;
        foreach (var item in savedTracks)
        {
            playlistContent += counter + ". " + item.TrackName + " - " + item.AlbumName + " - " + item.ArtistName + "\n";
            counter++;
        }

        string prompt = "Aşağıdaki Spotify çalma listesini genel olarak tüm şarkıları bir tutarak analiz etmeni istiyorum. "
                        + "Analiz, özellikle oynatma listesinin ne kadar catchy, hooky, punchy, engaging, appealing ve charming olduğu üzerine odaklansın. "
                        + "Oynatma listesini melodik yapı, ritim, sözler, prodüksiyon kalitesi, vokal performansı ve genel atmosfer açısından değerlendir. "
                        + " LÜTFEN Yanıtın SADECE 3 KISA UZUNLUKTA PARAGRAF Olsun!!!! "
                        + "ŞARKILAR TEKER TEKER DEĞERLENDIRME YAPMA! Sadece hepsini bir arada genel itibariyle değerlendir. "
                        + "Önemli: Yıldız işareti ASLA KULLANMA. yıldız karakteri kullanma. "
                        + "Şu sorulara cevap verir şekilde analiz yap: "
                        + "- Bu oynatma listesinin dinleyiciyi çeken özelliği nedir? "
                        + "- Sence bu oynatma listesindeki en iyi şarkı hangisi. bir tane en iyi şarkı, mutlaka seç.? "
                        + "- Duygusal olarak nasıl bir bağ kuruyor? "
                        + "- Bu oynatma listesi neden kulağa hoş geliyor (veya gelmiyor)? "
                        + "Tüm şarkıların sonunda genel bir özet yap: "
                        + "Bu playlist neden çekici veya değil? Dinleyiciyi içine çeken bütünsel bir 'charm' var mı? "
                        + "🎵 Playlist İçeriği (Şarkı - Albüm - Şarkıcı şeklinde sıralandı):\n"
                        + playlistContent
                        + "Yanıtın analitik olduğu kadar yaratıcı ve sezgisel de olsun — bir müzik eleştirmeni gibi düşün. DUYGUSAL bir hal takınabilirsin. "
                        + "Ama teknik terimlerle sınırlı kalmadan, dinleyici deneyimini de yansıt. LÜTFEN Yanıtın SADECE 3 KISA UZUNLUKTA PARAGRAF Olsun!!!!"
                        + "ÖNEMLİ NOT: Soruma English dilinde yanıt oluştur!!!!";

        if (!string.IsNullOrEmpty(prompt))
        {
            string yanit = await Task.Run(() => GeminiAPICagir(prompt));

            loadingLayout.IsVisible = false;

            lblAnalyzeResult.Text = yanit;
            lblResultHeading.Text = "Gemini's Playlist Analysis";

            Preferences.Set("AnalyzeContent", yanit);
            Preferences.Set("OldPlaylistCode", sPlaylistCode);

            remainingAnalyses--;
            Preferences.Set("RemainingAnalyses", remainingAnalyses);

            UpdateLimitLabel(remainingAnalyses);

            resultsFrame.IsVisible = true;

            if (remainingAnalyses > 0)
            {
                btnAnalyzeGemini.IsEnabled = true;
            }
            else
            {
                btnAnalyzeGemini.IsEnabled = false;
                btnAnalyzeGemini.Text = "❌ Daily Limit Reached";
            }
        }
    }
}