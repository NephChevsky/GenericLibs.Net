namespace SshLib
{
	public interface ISshService
	{
		Task<SshCommandResult> ExecuteCommandAsync(string host, string username, string password, string command);
		Task<SshCommandResult> ExecutePowerShellCommandAsync(string host, string username, string password, string powershellCommand);
	}
}
