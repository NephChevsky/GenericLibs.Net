namespace LocalStorageLib
{
	public class LocalStorageOptions
	{
		public const string SectionName = "LocalStorage";

		/// <summary>Root directory; each container is a subdirectory of it.</summary>
		public string RootPath { get; set; } = string.Empty;
	}
}
