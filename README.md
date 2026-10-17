# SimpVid Gist WPF - Extract `transcript`, `summarize`, and `export to .txt` for any YouTube videos with ease. All you need is a YouTube `video link`. 

<table>
  <tr>
    <th colspan="2">General Information</th>
  </tr>
  <tr>
    <td>Requirements</td>
    <td><code>.NET 10</code></td>
  </tr>
  <tr>
    <td>Credits</td>
    <td>Gemini &amp; RationalZombie</td>
  </tr>
  <tr>
    <td>Minimum Supported OS</td>
    <td><code>Windows 10, version 1607</code> — the minimum OS version supported by <code>.NET 10</code></td>
  </tr>
</table>

We all know that YouTube has a `Ask Gemini` Feature.

<img width="132.4" height="190.6" alt="Screenshot of YouTube Ask Gemini Interface with Gemini suggestions" src="https://github.com/user-attachments/assets/673cbc12-1a32-491a-9015-a220435c7aaf" />

- SimpVid Gist brings the feature of summarization (currently) to `Windows Desktop users`. More importantly, you can directly export transcript and/or summary `into a txt file, which YouTube currently cannot.`

- You can experience the modern Fluent Design, and even Mica transparency effect on Windows 11.

- Here's what it looks like on `version 1.4`. Recently, the UI is revamped, hope you like it.

<img width="398" height="665" alt="SimpVid Gist v1.4 showing YouTube link been inserted and a successfully extracted YouTube transcript in a textbox" src="https://github.com/user-attachments/assets/697dc829-e52d-4d03-b583-7c79168fea5f" />

## Technical Details

By combining the powerful `YoutubeExplode` parsing library with an adaptive AI interface, this tool allows you to extract multi language transcripts with one click and connect to any OpenAI-compliant AI platform (such as OpenAI, DeepSeek, Groq, Ollama, etc.) for summarization either in the cloud or locally. 

## What You Need to Know

SimpVid Gist will not store your AI API keys to prevent data leak.

> 💡 **Tip**: If you are using a local Ollama or LM Studio instance, authentication is typically not required. You can type any dummy characters (e.g., `sk-123`) into the **AI API key** field to bypass the empty value check.

## Development & Compilation Environment

- **Development Tool**: Visual Studio (any version with `.slnx` solution file support)
- **Target Framework**: .NET Framework 10.0
- **Core NuGet Dependencies**:
  - `YoutubeExplode` (For parsing YouTube videos and captions)
  - `System.Text.Json` (For processing universal AI JSON payloads)
  - `AngleSharp`

## License

This project is open-source and licensed under the **MPL v2.0 License**.
