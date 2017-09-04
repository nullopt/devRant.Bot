namespace devRant.Bot.Managers
{
	using System;
	using System.Linq;
	using System.Net.Http;
	using System.Runtime.CompilerServices;
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
		
		[Command("surprise")]
		[Description("Gets a random rant")]
		public async Task Surprise(CommandContext ctx)
		{
			using (var httpClient = new HttpClient())
			{
				var client = DevRantClient.Create(httpClient);

				var rant = client.GetSurprise().Result;

				var rantEmbed = new DiscordEmbedBuilder
					{
						Author = new DiscordEmbedBuilder.EmbedAuthor
						{
							IconUrl = GetIcon(rant.Rant.UserAvatar.Image),
							Name = $"{rant.Rant.UserName} [{rant.Rant.UserScore}]",
							Url = GetUrl(rant.Rant.UserName)
						},
						ThumbnailUrl = GetIcon(rant.Rant.UserAvatar.Image),
						Timestamp = new DateTimeOffset(DateTime.Now),
						Color = new DiscordColor(rant.Rant.UserAvatar.Background),
						Description = rant.Rant.Content,
						ImageUrl = rant.Rant.AttachedImage?.URL,
						Footer = new DiscordEmbedBuilder.EmbedFooter
							         {
										 Text = $"Rant ID: {rant.Rant.Id}"
							         }
					};

				await ctx.RespondAsync($"", embed: rantEmbed);
			}
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
						c = client.GetRants(Sort.Top, 1);
						break;
					case "algo":
						c = client.GetRants(Sort.Algo, 1);
						break;
					default:
						c = client.GetRants(Sort.Recent, 1);
						break;
				}
				var rant = c.Result.Rants.First();

				var rantEmbed = new DiscordEmbedBuilder
					                {
						                Author = new DiscordEmbedBuilder.EmbedAuthor
							                         {
								                         IconUrl = GetIcon(rant.UserAvatar.Image), Name = $"{rant.UserName} [{rant.UserScore}]",
								                         Url = GetUrl(rant.UserName)
							                         },
						                ThumbnailUrl = GetIcon(rant.UserAvatar.Image), Timestamp = new DateTimeOffset(DateTime.Now),
						                Color = new DiscordColor(rant.UserAvatar.Background), Description = rant.Content
					                };

				if (rant.Tags.Any())
				{
					var tags = rant.Tags.Aggregate("", (current, tag) => current + $"[{tag}]");
					rantEmbed.WithFooter($"Tags: {tags}", "https://www.google.com/s2/favicons?domain=www.devrant.io");
				}

				if (rant.AttachedImage != null) rantEmbed.ImageUrl = rant.AttachedImage.URL;

				rantEmbed.Build();
				await ctx.RespondAsync("", embed: rantEmbed);

				var comments = client.GetRant(rant.Id).Result.Comments;

				if (comments.Count == 0) return;

				DiscordEmbedBuilder CommentEmbed(int commentNumber)
				{
					return new DiscordEmbedBuilder
						       {
							       Author = new DiscordEmbedBuilder.EmbedAuthor
								                {
									                IconUrl = GetIcon(comments[commentNumber].UserAvatar.Image),
									                Name = $"{comments[commentNumber].UserName} [{comments[commentNumber].UserScore}]",
									                Url = GetUrl(comments[commentNumber].UserName)
								                },
							       Color = new DiscordColor(comments[commentNumber].UserAvatar.Background),
							       Description = comments[commentNumber].Content
						       };
				}

				var counter = 1;

				CommentEmbed(counter).Build();

				var commentSection = ctx.RespondAsync($"Comment #{counter}/{comments.Count}", embed: CommentEmbed(counter - 1))
					.Result;
				var up = UpEmoji(ctx);
				var down = DownEmoji(ctx);

				await commentSection.CreateReactionAsync(up);
				await commentSection.CreateReactionAsync(down);

				ctx.Client.MessageReactionAdded += eventArgs =>
					{
						if (eventArgs.User.IsBot) return Task.CompletedTask;
						if (eventArgs.Message.Id != commentSection.Id) return Task.CompletedTask;
						//if (eventArgs.User.Id != ctx.Member.Id) return Task.CompletedTask;

						eventArgs.Message.DeleteReactionAsync(eventArgs.Emoji, eventArgs.User);

						if (eventArgs.Emoji.Equals(up))
						{
							if (counter < 0)
							{
								counter = 0;
								return Task.CompletedTask;
							}
							counter--;
							commentSection.ModifyAsync($"Comment #{counter}/{comments.Count}", CommentEmbed(counter - 1));
							Task.Delay(250);
							return Task.CompletedTask;
						}
						if (!eventArgs.Emoji.Equals(down)) return Task.CompletedTask;
						if (counter < comments.Count + 1)
						{
							counter++;
							commentSection.ModifyAsync($"Comment #{counter}/{comments.Count}", CommentEmbed(counter - 1));
							Task.Delay(250);
							return Task.CompletedTask;
						}
						if (counter == comments.Count + 1) return Task.CompletedTask;
						Task.Delay(250);
						return Task.CompletedTask;
					};
			}
		}

		[Command("search")]
		[Description("Searches for x amount of rants")]
		public async Task Search(CommandContext ctx,
		                         [Description("Search term")] string term,
								 [Description("Start rant #")] int startingRant = 1)
		{
			if (startingRant < 1)
			{
				await ctx.RespondAsync("Last argument has to be above 1.");
				return;
			}

			using (var httpClient = new HttpClient())
			{
				var client = DevRantClient.Create(httpClient);

				var rants = client.SearchRants(term).Result;
				if (!rants.Success || rants.Results.Count < 1)
				{
					await ctx.RespondAsync("No rants with this tag found.");
					return;
				}

				var counter = startingRant;

				DiscordEmbedBuilder RantEmbed(int rantNumber)
				{
					return new DiscordEmbedBuilder
						       {
							       Author = new DiscordEmbedBuilder.EmbedAuthor
								                {
									                IconUrl = GetIcon(rants.Results[rantNumber].UserAvatar.Image),
									                Name = $"{rants.Results[rantNumber].UserName} [{rants.Results[rantNumber].UserScore}]",
									                Url = GetUrl(rants.Results[rantNumber].UserName)
								                },
							       ThumbnailUrl = GetIcon(rants.Results[rantNumber].UserAvatar.Image),
							       Timestamp = new DateTimeOffset(DateTime.Now),
							       Color = new DiscordColor(rants.Results[rantNumber].UserAvatar.Background),
							       Description = rants.Results[rantNumber].Content,
								   ImageUrl = rants.Results[rantNumber].AttachedImage == null ? null : rants.Results[rantNumber].AttachedImage.URL
						       };
				}
				
				var message = ctx.RespondAsync($"Rant #{counter}/{rants.Results.Count}", embed: RantEmbed(counter - 1)).Result;

				if (rants.Results.Count > 1)
				{
					var up = UpEmoji(ctx);
					var down = DownEmoji(ctx);

					await message.CreateReactionAsync(up);
					await message.CreateReactionAsync(down);
					ctx.Client.MessageReactionAdded += eventArgs =>
						{
							if (eventArgs.User.IsBot || eventArgs.Message.Id != message.Id) return Task.CompletedTask;
							eventArgs.Message.DeleteReactionAsync(eventArgs.Emoji, eventArgs.User);
							//if (eventArgs.User.Id != ctx.Member.Id) return Task.CompletedTask;

							if (eventArgs.Emoji.Equals(up))
							{
								if (counter < 0)
								{
									counter = 0;
									return Task.CompletedTask;
								}
								counter--;
								message.ModifyAsync($"Rant #{counter}/{rants.Results.Count}", RantEmbed(counter - 1));
								Task.Delay(250);
								return Task.CompletedTask;
							}
							if (!eventArgs.Emoji.Equals(down)) return Task.CompletedTask;
							if (counter < rants.Results.Count + 1)
							{
								counter++;
								message.ModifyAsync($"Rant #{counter}/{ rants.Results.Count}", RantEmbed(counter - 1));
								Task.Delay(250);
								return Task.CompletedTask;
							}
							if (counter == rants.Results.Count + 1) return Task.CompletedTask;
							Task.Delay(250);
							return Task.CompletedTask;
						};
				}
			}
		}

		#endregion

		#region Methods

		private static DiscordEmoji DownEmoji(CommandContext ctx)
		{
			return DiscordEmoji.FromUnicode(ctx.Client, "🔽");
		}

		private static string GetIcon(string s)
		{
			return $"https://avatars.devrant.io/{s}";
		}

		private static string GetUrl(string s)
		{
			return $"https://www.devrant.io/users/{s}";
		}

		private static DiscordEmoji UpEmoji(CommandContext ctx)
		{
			return DiscordEmoji.FromUnicode(ctx.Client, "🔼");
		}

		#endregion
	}
}