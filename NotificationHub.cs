using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using RYT.Models.Entities;
using RYT.Models.ViewModels;
using System.Threading.Tasks;
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        Console.WriteLine($"User connected: {userId}");
        await base.OnConnectedAsync();
    }

    public async Task SendNotificationToUser(string userId, string message)
    {
        await Clients.User(userId).SendAsync("ReceiveNotification", message);
    }
}




