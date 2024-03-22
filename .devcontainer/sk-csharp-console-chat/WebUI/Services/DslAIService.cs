using DslCopilot.WebUI.Options;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Plugins;

namespace DslCopilot.WebUI.Services
{
  public class DslAIService
  {

    WebCodeGenChat _chat;
    public DslAIService()
    {

    }

    public IAsyncEnumerable<StreamingChatMessageContent> AskAI(string prompt, string antlrDef, string promptName)
    {
      _chat.
    }
  }
}
