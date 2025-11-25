using Microsoft.AspNetCore.SignalR;

namespace loyalityAgent2._0.Hubs
{
    public class WorkflowHub : Hub
    {
        public async Task SendWorkflowUpdate(string connectionId, string step, string message, object? data = null)
        {
            await Clients.Client(connectionId).SendAsync("ReceiveWorkflowUpdate", step, message, data);
        }
    }
}
