namespace AzureBlobLib
{
	public class AzureBlobOptions
	{
		public const string SectionName = "AzureBlob";

		public string ConnectionString { get; set; } = string.Empty;

		/// <summary>Create containers on first use if they do not exist.</summary>
		public bool CreateContainerIfNotExists { get; set; } = true;
	}
}
