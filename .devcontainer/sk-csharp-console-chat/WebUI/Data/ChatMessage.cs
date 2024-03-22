using Microsoft.AspNetCore.Components;

namespace DslCopilot.WebUI.Data
{
  public class ChatMessage
  {
    public string? Message { get; set; }
    public string? Response { get; set; }
    public string? SelectedLangauge { get; set; }
  }
}
