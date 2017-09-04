namespace DevRantBot.Managers
{
	using System;
	using System.Threading.Tasks;

	using DSharpPlus;
	using DSharpPlus.EventArgs;

	internal class EventManager
	{
		#region Public Methods and Operators

		public Task ClientError(ClientErrorEventArgs e)
		{
			e.Client.DebugLogger.LogMessage(LogLevel.Error,
				"Client Error",
				$"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}",
				DateTime.Now);
			return Task.CompletedTask;
		}

		public Task OnMessage(MessageCreateEventArgs e)
		{
			//			if (e.Channel.IsPrivate) return Task.CompletedTask;
			//			if (e.Author.IsBot) return Task.CompletedTask;

			return Task.CompletedTask;
		}

		public Task Ready(ReadyEventArgs e)
		{
			e.Client.DebugLogger.LogMessage(LogLevel.Info, "Ready", "Client is ready to process events.", DateTime.Now);
			return Task.CompletedTask;
		}

		public Task SocketError(SocketErrorEventArgs e)
		{
			e.Client.DebugLogger.LogMessage(LogLevel.Error,
				"Socket Error",
				$"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}",
				DateTime.Now);
			return Task.CompletedTask;
		}

		#endregion

		public Task MessageReactionAdded(MessageReactionAddEventArgs e)
		{
			e.Client.DebugLogger.LogMessage(LogLevel.Debug, "ReactionAdded", $"Message ID: {e.Message.Id}. Emoji ID: {e.Emoji.Id}.", DateTime.Now);
			return Task.CompletedTask;
		}

		public Task MessageReactionRemoved(MessageReactionRemoveEventArgs e)
		{
			e.Client.DebugLogger.LogMessage(LogLevel.Debug, "ReactionRemoved", $"Message ID: {e.Message.Id}. Emoji ID: {e.Emoji.Id}.", DateTime.Now);
			return Task.CompletedTask;
		}
	}
}