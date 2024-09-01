using FirebaseAdmin.Messaging;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.SignalR;
using RYT.Models.Entities;
using RYT.Models.ViewModels;
using RYT.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace RYT.Services.NotificationSaga
{

    public class FirebaseService : IFirebaseService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly IHubContext<NotificationHub> _hubContext;
        public FirebaseService(FirestoreDb firestoreDb, IHubContext<NotificationHub> hubContext)
        {
            _firestoreDb = firestoreDb;
            _hubContext = hubContext;
        }

        public async Task<int> AddNotification(FireBaseNotfication notification)
        {
            await EnsureUserCollectionExists(notification.userId);
            notification.status = "sent";
            // Get the last used NotificationId for the user
            int lastNotificationId = await GetLastNotificationId(notification.userId);
            notification.notificationId= lastNotificationId+1;
            var notificationsCollection = _firestoreDb.Collection($"usersNotification/{notification.userId}/notifications");
            DocumentReference notificationRef = notificationsCollection.Document(notification.notificationId.ToString());
            await notificationRef.SetAsync(notification);

            if (notificationRef!= null)
            {
                await _hubContext.Clients.User(notification.userId).SendAsync("ReceiveNotification", notification);
                return notification.notificationId;
            }
            return 0;
        }
        public async Task<int> GetUnreadNotifications(string userId)
        {
            await EnsureUserCollectionExists(userId);

            var notificationsCollection = _firestoreDb.Collection($"usersNotification/{userId}/notifications");
            var query = await notificationsCollection.WhereNotEqualTo("status", "read").OrderBy("notificationId").GetSnapshotAsync();

            // Process query results
            int notifications = query.Documents
                .Select(doc => doc.ConvertTo<FireBaseNotfication>())
                .ToList()
                .Count();
            return notifications;
        }

        public async Task<IEnumerable<FireBaseNotfication>> GetNotifications(string userId)
        {
            await EnsureUserCollectionExists(userId);

            var notificationsCollection = _firestoreDb.Collection($"usersNotification/{userId}/notifications");
            var query = await notificationsCollection.OrderBy("notificationId").GetSnapshotAsync();

            // Process query results
            var notifications = query.Documents
                .Select(doc => doc.ConvertTo<FireBaseNotfication>())
                .ToList();
            return notifications;
        }
        public async Task<int> ReadNotification(string userId, int notificationId)
        {
            try
            {
                // Get a reference to the notification document
                DocumentReference notificationRef = _firestoreDb.Collection($"usersNotification/{userId}/notifications").Document(notificationId.ToString());
                // Create a dictionary to hold the "Status" field and its updated value
                Dictionary<string, object> updates = new Dictionary<string, object>
                {
                    { "status", "read" }
                };
                // Update the "Status" field in the document
                await notificationRef.UpdateAsync(updates);
                return 1;
            }
            catch (Exception ex)
            {
                // Log the exception or handle it appropriately
                Console.WriteLine($"Error updating notification status: {ex.Message}");

                // If the update failed, return false
                return 0;
            }
        }
        public async Task<int> DeleteNotification(string userId, int notificationId)
        {
            await EnsureUserCollectionExists(userId);

            DocumentReference notificationRef = _firestoreDb.Document($"usersNotification/{userId}/notifications/{notificationId}");
            var deleteResult = await notificationRef.DeleteAsync();

            return deleteResult != null ? 1 : 0;
        }

        public async Task EnsureUserCollectionExists(string userId)
        {
            var userCollectionRef = _firestoreDb.Collection($"usersNotification");
            var userDocumentRef = userCollectionRef.Document(userId);

            var snapshot = await userDocumentRef.GetSnapshotAsync();
            if (!snapshot.Exists)
            {
                // User collection does not exist, create it.
                await userDocumentRef.CreateAsync(new Dictionary<string, object>());
            }
        }
        public async Task<FireBaseNotfication> GetNotificationById(string userId, int notificationId)
        {
            await EnsureUserCollectionExists(userId);

            var notificationRef = _firestoreDb.Document($"usersNotification/{userId}/notifications/{notificationId}");
            var snapshot = await notificationRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                return snapshot.ConvertTo<FireBaseNotfication>();
            }

            return null;
        }
        public async Task<int> GetLastNotificationId(string userId)
        {
            var notificationsCollection = _firestoreDb.Collection($"usersNotification/{userId}/notifications");
            var query = await notificationsCollection.OrderByDescending("notificationId").Limit(1).GetSnapshotAsync();

            var lastNotification = query.Documents.FirstOrDefault();
            if (lastNotification != null)
            {
                return lastNotification.GetValue<int>("notificationId");
            }

            return 0; // Return 0 if no notifications exist yet.
        }
    }
}