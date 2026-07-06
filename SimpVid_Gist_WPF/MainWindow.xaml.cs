using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YoutubeExplode;
using YoutubeExplode.Videos.ClosedCaptions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;


namespace SimpVid_Gist_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();
        private readonly HttpClient _httpClient = new HttpClient();
        public MainWindow()
        {
            InitializeComponent();

            // Hook up the click event to our handler
            ExtractButton.Click += ExtractButton_Click;
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            string videoInput = UrlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(videoInput))
            {
                MessageBox.Show("Please enter a valid YouTube Video URL or ID", "Invalid Video Input", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // UI Feedback while working
            ExtractButton.IsEnabled = false;
            TranscriptTextBox.Text = "Fetching transcript tracks...";
            try
            {
                // 1. Fetch the caption tracks available for the video
                var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(videoInput);

                // 2. Try to grab English ("en") or grab the first available track
                var trackInfo = trackManifest.GetByLanguage("en") ?? trackManifest.Tracks[0];

                if (trackInfo != null)
                {
                    TranscriptTextBox.Text = "Downloading transcript...";

                    // 3. Download the actual transcript contents
                    var closedCaptionTrack = await _youtubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                    // 4. Compile the text segments together
                    var transcriptBuilder = new StringBuilder();
                    foreach (var caption in closedCaptionTrack.Captions)
                    {
                        // Exclude empty entries or music tags like [Music] if desired
                        if (!string.IsNullOrWhiteSpace(caption.Text))
                        {
                            transcriptBuilder.AppendLine(caption.Text);
                        }
                    }

                    // 5. Output to the UI
                    TranscriptTextBox.Text = transcriptBuilder.ToString();

                    SummarizeButton.IsEnabled = true;
                }
                else
                {
                    TranscriptTextBox.Text = "No transcript or captions found for this video.";
                }
            }
            catch (Exception ex)
            {
                TranscriptTextBox.Text = $"Error retrieving transcript: {ex.Message}";
            }
            finally
            {
                // Always re-enable the button when done
                ExtractButton.IsEnabled = true;
            }
        }
        /// <summary>
        /// Step 2: Handle Self-service AI summarization
        /// </summary>

        private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Securely grab the key from PasswordBox
            string apiKey = ApiKeyTextBox.Password.Trim();
            string transcript = TranscriptTextBox.Text.Trim();
            string apiUrl = BaseUrlTextBox.Text.Trim();
            string modelName = ModelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter your AI API key first", "AI API key Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(transcript) || transcript.StartsWith("Error"))
            {
                MessageBox.Show("Please fetch a valid video transcript before summarizing.", "Transcript Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                MessageBox.Show("Please enter the AI Base URL.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(modelName))
            {
                MessageBox.Show("Please enter the Model Name.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Lock UI elements during network traffic
            SummarizeButton.IsEnabled = false;
            ExtractButton.IsEnabled = false;
            SummaryTextBox.Text = "Analyzing transcript & generating summary...";

            try
            {
                string summaryResult = await CallAiApiAsync(apiKey, transcript, apiUrl, modelName);
                SummaryTextBox.Text = summaryResult;
            }
            catch (Exception ex)
            {
                SummaryTextBox.Text = $"AI Error: {ex.Message}";
            }
            finally
            {
                SummarizeButton.IsEnabled = true;
                ExtractButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Universal HTTP wrapper compatible with standard AI endpoints
        /// </summary>
        private async Task<string> CallAiApiAsync(string apiKey, string transcriptContent, string apiUrl, string modelName)
        {
            // Build a standard OpenAI compatible Payload
            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = "You are an expert assistant. Summarize the following YouTube transcript into clear, structured paragraphs. You may use key bullet points." },
                    new { role = "user", content = transcriptContent }
                },
                temperature = 0.5
            };
            string jsonPayload = JsonSerializer.Serialize(requestBody);

            using (var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)) // Use user specified URL
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"({response.StatusCode}) Details: {responseString}");
                }

                using (JsonDocument doc = JsonDocument.Parse(responseString))
                {
                    JsonElement root = doc.RootElement;
                    string rawSummary = root.GetProperty("choices")[0]
                                            .GetProperty("message")
                                            .GetProperty("content")
                                            .GetString();
                    return rawSummary?.Trim() ?? "AI returned an empty response.";
                }
            }
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DockPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}