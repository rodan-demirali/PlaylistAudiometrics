namespace PlaylistAudiometrics;

public partial class PAquiz : ContentPage
{
	public PAquiz()
	{
		InitializeComponent();
	}

    private async void btnPlayQuiz_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new QuizGameSettings());

    }
}