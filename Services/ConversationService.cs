using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class ConversationService
{
    private readonly AppDbContext _db;

    public ConversationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation> GetOrCreateConversationAsync(long customerId)
    {
        var existing = await _db.Conversations
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.LastMessageAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            return existing;
        }

        var conversation = new Conversation
        {
            CustomerId = customerId,
            CurrentState = "idle",
            ContextData = "{}",
            SessionStartedAt = DateTimeOffset.UtcNow,
            LastMessageAt = DateTimeOffset.UtcNow
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();
        return conversation;
    }

    public async Task UpdateStateAsync(long conversationId, string newState, Dictionary<string, object>? contextPatch = null, bool resetContext = false)
    {
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation is null) return;

        conversation.CurrentState = newState;
        conversation.LastMessageAt = DateTimeOffset.UtcNow;

         if (resetContext)
        {
            conversation.ContextData = "{}";
        }
        else if (contextPatch is not null && contextPatch.Count > 0)
        {
            var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(conversation.ContextData)
                            ?? new Dictionary<string, object>();

            foreach (var kvp in contextPatch)
            {
                existing[kvp.Key] = kvp.Value;
            }

            conversation.ContextData = JsonSerializer.Serialize(existing);
        }

        await _db.SaveChangesAsync();
    }

    public async Task LogMessageAsync(long conversationId, string direction, string messageType, string? content, string? rawPayloadJson)
    {
        _db.Messages.Add(new Message
        {
            ConversationId = conversationId,
            Direction = direction,
            MessageType = messageType,
            Content = content,
            Payload = rawPayloadJson,
            SentAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
