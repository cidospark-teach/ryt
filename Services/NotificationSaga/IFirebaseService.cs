using Microsoft.AspNetCore.Mvc;
using RYT.Models.Entities;
using RYT.Models.ViewModels;

namespace RYT.Services.NotificationSaga
{
    public interface IFirebaseService
    {
        Task<int> AddNotification(FireBaseNotfication notification);
        Task<IEnumerable<FireBaseNotfication>> GetNotifications(string userId);
        Task<int> ReadNotification(string userId, int notificationId);
        Task<int> DeleteNotification(string userId, int notificationId);
        Task<FireBaseNotfication> GetNotificationById(string userId, int notificationId);
        Task<int> GetUnreadNotifications(string userId);
    }

}
