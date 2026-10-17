using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using YoutubeExplode;


namespace SimpVid_Gist_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _saveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpVid Gist", "userdata.txt");
        public MainWindow()
        {
            InitializeComponent();

            // Load previous user configurations
            LoadFromAppData();
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            string videoInput = UrlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(videoInput))
            {
                MessageBox.Show("Please enter a valid YouTube Video URL or ID", "Invalid Video Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // UI Feedback while working
            ExtractButton.IsEnabled = false;
            ExtractButton.Content = "Fetching transcript tracks...";
            try
            {
                // 1. Fetch the caption tracks available for the video
                var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(videoInput);

                // 2. Grab track with specified language
                // Default Fallback: English
                string targetLangCode = (LanguageCodeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
                // ?. Means if not null, then...

                var trackInfo = trackManifest.GetByLanguage(targetLangCode)
                                ?? trackManifest.Tracks.FirstOrDefault();

                if (trackInfo != null)
                {
                    ExtractButton.Content = "Downloading transcript...";

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
                    TranscriptTextBox.Text = $"No transcript or captions found for this video in language {targetLangCode}.";
                }
            }
            catch (Exception ex)
            {
                TranscriptTextBox.Text = $"Error retrieving transcript:\n{ex.Message}";
            }
            finally
            {
                // Always re-enable the button when done
                ExtractButton.IsEnabled = true;
                ExtractButton.Content = "Extract Transcript";
            }
        }

        /// <summary>
        /// Handle Self-service AI summarization
        /// </summary>
        private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
        {
            SummaryTextBox.Text = "";
            // Grab user information
            string apiKey = ApiKeyTextBox.Password.Trim();
            string transcript = TranscriptTextBox.Text.Trim();
            string apiUrl = BaseUrlTextBox.Text.Trim();
            string modelName = ModelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                SummaryTextBox.AppendText("Sorry, an AI API key is required. Please insert your AI API key.\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(transcript) || transcript.StartsWith("Error"))
            {
                SummaryTextBox.AppendText("Sorry, please fetch a valid video transcript before summarizing.\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                SummaryTextBox.AppendText("Sorry, please enter the AI Base URL.\nYou can click \"Save AI Base URL and Model Name\" afterwards.\n");
                return;
            }

            if (string.IsNullOrWhiteSpace(modelName))
            {
                SummaryTextBox.AppendText("Sorry, please enter the Model Name.\nYou can click \"Save AI Base URL and Model Name\" afterwards.\n");
                return;
            }
            // Lock UI elements during network traffic
            SummarizeButton.IsEnabled = false;
            ExtractButton.IsEnabled = false;
            SummarizeButton.Content = "Waiting for server to respond...";
            try
            {
                SummaryTextBox.Text = "";
                // Await each chunk as it arrives from the server
                await foreach (string chunk in CallAiApiStreamAsync(apiKey, transcript, apiUrl, modelName))
                {
                    // Use AppendText for better performance instead of +=
                    SummaryTextBox.AppendText(chunk);
                }
            }
            catch (Exception ex)
            {
                SummaryTextBox.Text = $"AI Error:\n{ex.Message}";
            }
            finally
            {
                SummarizeButton.IsEnabled = true;
                ExtractButton.IsEnabled = true;
                SummarizeButton.Content = "Re-summarize Transcript";

            }
        }

        /// <summary>
        /// Universal HTTP wrapper for standard AI endpoints, now supporting Streaming!
        /// </summary>
        private async IAsyncEnumerable<string> CallAiApiStreamAsync(string apiKey, string transcriptContent, string apiUrl, string modelName)
        {
            var requestBody = new JsonObject
            {
                ["model"] = modelName,
                ["messages"] = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = "You are an expert AI summarizer. Summarize the following YouTube transcript into clear, structured paragraphs. You may use bullet points. " +
                "NOTICE: 1. The VERY FIRST line of your response MUST be a single short sentence (under 80 characters, no quotes, no colons) describing the overall topic of the video. Leave one blank line after the provided summary."
                +"2. Avoid using Markdown. Respond with Plain Text."
            },
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = transcriptContent
            }
        },
                ["temperature"] = 0.5,
                ["stream"] = true // 1. Tell the API we want a stream back
            };

            string jsonPayload = requestBody.ToJsonString();

            using (var request = new HttpRequestMessage(HttpMethod.Post, apiUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // 2. Use ResponseHeadersRead to start processing the moment the server connects, instead of waiting for the full download
                using (HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"({response.StatusCode})\nDetails:\n{error}");
                    }

                    // 3. Read the stream line by line
                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            string line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            // 4. Server-Sent Events (SSE) usually prefix data with "data:"
                            if (line.StartsWith("data:"))
                            {
                                string data = line.Substring(5).Trim();

                                // The stream tells us it's finished by sending [DONE]
                                if (data == "[DONE]") break;

                                JsonDocument doc;
                                try
                                {
                                    doc = JsonDocument.Parse(data);
                                }
                                catch
                                {
                                    continue; // Skip any malformed fragments safely
                                }

                                using (doc)
                                {
                                    var choices = doc.RootElement.GetProperty("choices");
                                    if (choices.GetArrayLength() > 0)
                                    {
                                        var delta = choices[0].GetProperty("delta");

                                        // In streaming mode, the text is inside 'delta.content' instead of 'message.content'
                                        if (delta.TryGetProperty("content", out JsonElement contentElement))
                                        {
                                            yield return contentElement.GetString();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string dataToSave = BaseUrlTextBox.Text + "\n" + ModelTextBox.Text;
            SaveToAppData("userdata.txt", dataToSave);
        }

        /// <summary>
        /// Save user configurations
        /// </summary>

        private void SaveToAppData(string fileName, string content)
        {
            try
            {
                // 写入文件（此处使用 UTF-8 编码，若文件已存在则覆盖）
                File.WriteAllText(_saveFilePath, content, Encoding.UTF8);
                MessageBox.Show($"Successfully saved.\nAt: {_saveFilePath}\nThe next time you open SimpVid Gist, your data will be automatically read.", "", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n{ex.Message}", "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// Read user configurations
        /// </summary>
        private void LoadFromAppData()
        {
            try
            {
                // If file exists, read the configuration.
                if (File.Exists(_saveFilePath))
                {
                    // Read the file by line
                    string[] lines = File.ReadAllLines(_saveFilePath, Encoding.UTF8);

                    // Fill in textboxes
                    if (lines.Length >= 1)
                    {
                        BaseUrlTextBox.Text = lines[0];
                    }
                    if (lines.Length >= 2)
                    {
                        ModelTextBox.Text = lines[1];
                    }

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load save data:\n{ex.Message}", "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WriteInContent()
        {
            string transcript = TranscriptTextBox.Text.Trim();
            string summary = SummaryTextBox.Text.Trim();

            // The first line of the AI summary is the overall video info, used as the file name.
            string overallInfo = "";
            string detailedSummary = summary;
            if (!string.IsNullOrEmpty(summary))
            {
                string[] lines = summary.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                if (lines.Length > 0)
                {
                    overallInfo = lines[0].Trim();
                    if (lines.Length > 1)
                    {
                        detailedSummary = string.Join(Environment.NewLine, lines, 1, lines.Length - 1).Trim();
                    }
                }
            }

            // Build a safe file name from the overall info line.
            string exportTime = DateTime.Now.ToString("yyyy-MM-dd, HH_mm_ss");
            string baseFileName;
            string fileFirstLine="SimpVid Gist Export at " + exportTime.Replace("_",":");
            if (string.IsNullOrWhiteSpace(overallInfo) || overallInfo.StartsWith("Sorry"))
            {
                baseFileName = "SimpVid Gist Export at " + exportTime;
            }
            else
            {
                string safeName = overallInfo;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }
                baseFileName = safeName;
            }

            // Initialize file save dialog box.
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            saveFileDialog.DefaultExt = "txt";
            saveFileDialog.FileName = baseFileName;

            // If user clicked save...
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Put together two parts.
                    StringBuilder fileContent = new StringBuilder();
                    // First line of the file: overall video info (same text as the file name).
                    fileContent.AppendLine(string.IsNullOrWhiteSpace(overallInfo) ? $"{fileFirstLine}" : overallInfo + "\n" + fileFirstLine);
                    fileContent.AppendLine();

                    fileContent.AppendLine("========================================");
                    fileContent.AppendLine("           YOUTUBE TRANSCRIPT           ");
                    fileContent.AppendLine("========================================");
                    fileContent.AppendLine(transcript);

                    if (!string.IsNullOrEmpty(detailedSummary))
                    {
                        fileContent.AppendLine();
                        fileContent.AppendLine("========================================");
                        fileContent.AppendLine("               AI SUMMARY               ");
                        fileContent.AppendLine("========================================");
                        fileContent.AppendLine((string.IsNullOrWhiteSpace(detailedSummary) || detailedSummary.StartsWith("Sorry"))? "[No Summary Generated]" : detailedSummary);
                    }

                    // Write text to user-specified file path.
                    File.WriteAllText(saveFileDialog.FileName, fileContent.ToString(), Encoding.UTF8);

                    MessageBox.Show($"File Successfully Saved to\n{saveFileDialog.FileName}", "", MessageBoxButton.OK, MessageBoxImage.Information);

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export file: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        /// <summary>
        /// Save to *.txt file.
        /// </summary>

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            // If two fields are empty, do not export.
            if (!SummarizeButton.IsEnabled)
            {
                MessageBox.Show("Please extract transcript first", "Nothing Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                WriteInContent();
            }

        }

        private void Button_Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

    }
}