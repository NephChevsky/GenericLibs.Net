namespace HangFireLib
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class RecurringJobAttribute : Attribute
	{
		public string? JobId { get; set; }
		public string CronExpression { get; }

		public RecurringJobAttribute(string cronExpression)
		{
			CronExpression = cronExpression;
		}
	}
}
