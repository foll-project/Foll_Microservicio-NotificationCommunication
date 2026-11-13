namespace foll_notifications.NotificationCommunication.Application.Contracts;

public enum ConnectivityChangeCause
{
    HeartbeatTimeout = 1,
    PowerOff = 2,
    HeartbeatReceived = 3,
    PowerOn = 4
}
