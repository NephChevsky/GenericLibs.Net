namespace NotifierLib
{
	public interface INotifier
	{
		Task SendNotificationAsync(string message, CancellationToken cancellationToken = default);
	}
}
