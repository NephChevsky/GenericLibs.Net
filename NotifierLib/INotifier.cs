namespace NotifierLib
{
	public interface INotifier
	{
		public Task SendNotification(string message);
	}
}
