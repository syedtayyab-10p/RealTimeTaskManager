using Microsoft.AspNetCore.SignalR;

namespace TaskManager.Api.Hubs;

// This acts as the gateway pipeline for real-time client connections
public class TaskHub : Hub
{
    // We leave this empty for now because our Controller will push the messages.
    // In the future, you could add custom methods here if clients need to send messages directly up to the hub.
}
