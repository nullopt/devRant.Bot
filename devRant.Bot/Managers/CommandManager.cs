namespace DevRantBot.Managers
{
	using System;
	using System.Linq;
	using System.Net.Http;
	using System.Threading.Tasks;

	using devRant.NET;

	using DSharpPlus;
	using DSharpPlus.CommandsNext;
	using DSharpPlus.CommandsNext.Attributes;
	using DSharpPlus.CommandsNext.Exceptions;
	using DSharpPlus.Entities;

	internal class CommandManager
	{
		#region Public Methods and Operators

		public async Task CommandError(CommandErrorEventArgs e)
		{
			e.Context.Client.DebugLogger.LogMessage(LogLevel.Debug,
				"Command",
				$"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message}",
				DateTime.Now);

			if (e.Exception is ChecksFailedException)
			{
				var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

				var embed = new DiscordEmbedBuilder
					            {
						            Title = "Access denied",
						            Description = $"{emoji} You do not have the permissions required to execute this command.",
						            Color = new DiscordColor(0xFF0000)
					            };
				await e.Context.RespondAsync("", embed: embed);
			}
		}

		public Task CommandExecuted(CommandExecutionEventArgs e)
		{
			e.Context.Client.DebugLogger.LogMessage(LogLevel.Debug,
				"Command",
				$"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'",
				DateTime.Now);

			return Task.CompletedTask;
		}

		[Command("rant")]
		[Description("Gets a rant")]
		public async Task Rant(CommandContext ctx, [Description("[top | recent | algo]")] string args = "recent")
		{
			using (var httpClient = new HttpClient())
			{
				var client = DevRantClient.Create(httpClient);

				Task<RantsResponse> c;

				switch (args)
				{
					case "top":
						c = client.GetRants(Sort.Top, 1, 0);
						break;
					case "algo":
						c = client.GetRants(Sort.Algo, 1, 0);
						break;
					default:
						c = client.GetRants(Sort.Recent, 1, 0);
						break;
				}
				var rant = c.Result.Rants.First();

				string Icon(string s)
				{
					return $"https://avatars.devrant.io/{s}";
				}

				string Url(string s)
				{
					return $"https://www.devrant.io/users/{s}";
				}

				var rantEmbed = new DiscordEmbedBuilder
					                {
						                Author = new DiscordEmbedBuilder.EmbedAuthor
							                         {
								                         IconUrl = Icon(rant.UserAvatar.Image), Name = $"{rant.Username} [{rant.UserScore}]",
								                         Url = Url(rant.Username)
							                         },
						                ThumbnailUrl = Icon(rant.UserAvatar.Image), Timestamp = new DateTimeOffset(DateTime.Now),
						                Color = new DiscordColor(rant.UserAvatar.Background), Description = rant.Text
					                };

				if (rant.Tags.Any())
				{
					var tags = rant.Tags.Aggregate("", (current, tag) => current + $"[{tag}]");
					rantEmbed.WithFooter($"Tags: {tags}", "https://www.google.com/s2/favicons?domain=www.devrant.io");
				}

				if (rant.AttachedImage != null) rantEmbed.ImageUrl = rant.AttachedImage.URL;

				rantEmbed.Build();
				await ctx.RespondAsync("", embed: rantEmbed);

				var comments = client.GetRant(rant.ID).Result.Comments;

				if (comments.Count == 0) return;

				DiscordEmbedBuilder CommentEmbed(int commentNumber)
				{
					return new DiscordEmbedBuilder
						       {
							       Author = new DiscordEmbedBuilder.EmbedAuthor
								                {
									                IconUrl = Icon(comments[commentNumber].UserAvatar.Image),
									                Name = $"{comments[commentNumber].UserUsername} [{comments[commentNumber].UserScore}]",
									                Url = Url(comments[commentNumber].UserUsername)
								                },
							       Color = new DiscordColor(comments[commentNumber].UserAvatar.Background), Description = comments[commentNumber].Body
						       };
				}
				var counter = 1;

				CommentEmbed(counter).Build();

				var commentSection = ctx.RespondAsync($"Comment #{counter}/{comments.Count}", embed: CommentEmbed(counter - 1)).Result;


				var up = DiscordEmoji.FromUnicode(ctx.Client, "🔼");
				var down = DiscordEmoji.FromUnicode(ctx.Client, "🔽");

				await commentSection.CreateReactionAsync(up);
				await commentSection.CreateReactionAsync(down);

				ctx.Client.MessageReactionAdded += eventArgs =>
					{
						if (eventArgs.User.IsBot) return Task.CompletedTask;
						if (eventArgs.Message.Id != commentSection.Id) return Task.CompletedTask;
						if (eventArgs.User.Id != ctx.Member.Id) return Task.CompletedTask;

						eventArgs.Message.DeleteReactionAsync(eventArgs.Emoji, eventArgs.User);

						if (eventArgs.Emoji.Equals(up))
						{
							if (counter < 0)
							{
								counter = 0;
								return Task.CompletedTask;
							}
							counter--;
							commentSection.ModifyAsync($"Comment #{counter}/{comments.Count}", embed: CommentEmbed(counter - 1));
							Task.Delay(250);
							return Task.CompletedTask;
						}
						if (!eventArgs.Emoji.Equals(down)) return Task.CompletedTask;
						if (counter < comments.Count + 1)
						{
							counter++;
							commentSection.ModifyAsync($"Comment #{counter}/{comments.Count}", embed: CommentEmbed(counter - 1));
						}
						if (counter == comments.Count + 1)
						{
							return Task.CompletedTask;
						}
						Task.Delay(250);
						return Task.CompletedTask;
					};
			}
		}

		#endregion
	}
}