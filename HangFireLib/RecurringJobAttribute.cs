namespace HangFireLib
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class RecurringJobAttribute(string cronExpression) : Attribute
	{
		public string? JobId { get; set; }
		public string CronExpression { get; } = cronExpression;
	}
}
