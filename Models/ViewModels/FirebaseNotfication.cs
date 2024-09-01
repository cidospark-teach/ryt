using Google.Cloud.Firestore;
using RYT.Models.Entities;

namespace RYT.Models.ViewModels
{
    [FirestoreData]
    public class FireBaseNotfication
    {
        [FirestoreProperty]
        public int notificationId { get; set; } = int.MinValue;

        [FirestoreProperty]
        public string userId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string messages { get; set; } = string.Empty;

        [FirestoreProperty]
        public DateTime dateTime { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public string status { get; set; } = "sent";
    }

}

