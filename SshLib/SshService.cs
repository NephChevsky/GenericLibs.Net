using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace SshLib
{
	public class SshService(ILogger<SshService> logger) : ISshService
	{
		private readonly ILogger<SshService> _logger = logger;

		public Task<SshCommandResult> ExecuteCommandAsync(string host, string username, string password, string command)
		{
			try
			{
				using SshClient sshClient = new(host, username, password);
				sshClient.Connect();

				if (!sshClient.IsConnected)
				{
					_logger.LogError("SSH connection failed");
					return Task.FromResult(new SshCommandResult
					{
						Error = "SSH connection failed"
					});
				}

				SshCommand cmd = sshClient.CreateCommand(command);
				string result = cmd.Execute();
				string error = cmd.Error;

				sshClient.Disconnect();

				return Task.FromResult(new SshCommandResult
				{
					Output = result,
					Error = error
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to execute SSH command");
				return Task.FromResult(new SshCommandResult
				{
					Error = ex.Message
				});
			}
		}

		public async Task<SshCommandResult> ExecutePowerShellCommandAsync(string host, string username, string password, string powershellCommand)
		{
			string sshCommand = $"pwsh -NoProfile -ExecutionPolicy Bypass -Command \"{powershellCommand}\"";
			return await ExecuteCommandAsync(host, username, password, sshCommand);
		}
	}
}
