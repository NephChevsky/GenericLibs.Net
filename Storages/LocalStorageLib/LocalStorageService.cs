using System.Runtime.CompilerServices;
using StorageLib;

namespace LocalStorageLib
{
	public class LocalStorageService : IStorageService
	{
		private readonly string _root;

		public LocalStorageService(LocalStorageOptions options)
		{
			if (string.IsNullOrWhiteSpace(options.RootPath))
			{
				throw new InvalidOperationException($"{LocalStorageOptions.SectionName}:RootPath is not configured.");
			}
			_root = Path.GetFullPath(options.RootPath);
		}

		public async Task UploadAsync(string container, string path, Stream content, string? contentType = null, bool overwrite = true, CancellationToken cancellationToken = default)
		{
			var fullPath = Resolve(container, path);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
			await using var file = new FileStream(fullPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
			await content.CopyToAsync(file, cancellationToken);
		}

		public Task<Stream?> OpenReadAsync(string container, string path, CancellationToken cancellationToken = default)
		{
			var fullPath = Resolve(container, path);
			if (!File.Exists(fullPath))
			{
				return Task.FromResult<Stream?>(null);
			}
			try
			{
				return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
			}
			catch (FileNotFoundException)
			{
				return Task.FromResult<Stream?>(null);
			}
			catch (DirectoryNotFoundException)
			{
				return Task.FromResult<Stream?>(null);
			}
		}

		public Task<bool> ExistsAsync(string container, string path, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(File.Exists(Resolve(container, path)));
		}

		public Task<bool> DeleteAsync(string container, string path, CancellationToken cancellationToken = default)
		{
			var fullPath = Resolve(container, path);
			if (!File.Exists(fullPath))
			{
				return Task.FromResult(false);
			}
			File.Delete(fullPath);
			return Task.FromResult(true);
		}

		public async IAsyncEnumerable<string> ListAsync(string container, string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			var containerPath = Resolve(container, null);
			if (!Directory.Exists(containerPath))
			{
				yield break;
			}

			foreach (var file in Directory.EnumerateFiles(containerPath, "*", SearchOption.AllDirectories))
			{
				cancellationToken.ThrowIfCancellationRequested();
				var relative = Path.GetRelativePath(containerPath, file).Replace(Path.DirectorySeparatorChar, '/');
				if (prefix == null || relative.StartsWith(prefix, StringComparison.Ordinal))
				{
					yield return relative;
				}
			}
			await Task.CompletedTask;
		}

		/// <summary>Resolves container/path under the root, rejecting any path that escapes it.</summary>
		private string Resolve(string container, string? path)
		{
			var containerPath = EnsureInside(_root, container);
			return path == null ? containerPath : EnsureInside(containerPath, path);
		}

		private static string EnsureInside(string baseDir, string relative)
		{
			if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
			{
				throw new ArgumentException("Invalid path.", nameof(relative));
			}
			var full = Path.GetFullPath(Path.Combine(baseDir, relative));
			var baseWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar) ? baseDir : baseDir + Path.DirectorySeparatorChar;
			if (!full.StartsWith(baseWithSep, StringComparison.Ordinal))
			{
				throw new ArgumentException("Path escapes the storage root.", nameof(relative));
			}
			return full;
		}
	}
}
