namespace DevRantBot.Managers
{
	using System.IO;
	using System.Text;
	using System.Threading.Tasks;

	using Newtonsoft.Json;

	internal class JsonManager
	{
		#region Public Methods and Operators

		public static async Task<string> ParseJsonAsync(string path)
		{
			var json = "";
			using (var fs = File.OpenRead(path))
			using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
			{
				json = await sr.ReadToEndAsync();
			}

			return json;
		}

		#endregion
	}

	public class ConfigJson
	{
		#region Public Properties

		[JsonProperty("prefix")]
		public string Prefix { get; private set; }

		[JsonProperty("token")]
		public string Token { get; private set; }

		#endregion
	}
}