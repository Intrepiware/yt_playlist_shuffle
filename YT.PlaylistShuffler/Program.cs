using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YT.PlaylistShuffler
{
    class Program
    {
        const string PLAYLIST_WORKOUT_ITEMS = "PL1cTD7ZEXOq3WuIIN4XeGZLxgUqmuvaEs";

        static async Task Main(string[] args)
        {
            Console.WriteLine("YouTube Data API: Playlist Updates");
            Console.WriteLine("==================================");

            try
            {
                await Run();
            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static async Task Run()
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    // This OAuth 2.0 access scope allows for full read/write access to the
                    // authenticated user's account.
                    new[] { YouTubeService.Scope.Youtube },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("YT.PlaylistShuffler")
                );
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "YT.PlaylistShuffler"
            });

            await ShufflePlaylistVideos(youtubeService, PLAYLIST_WORKOUT_ITEMS);
        }


        private static async Task ShufflePlaylistVideos(YouTubeService youtubeService, string playlistId)
        {
            var playlistItems = new Dictionary<string, string>();
            var itemsRequest = youtubeService.PlaylistItems.List("snippet,contentDetails");
            itemsRequest.PlaylistId = playlistId;
            itemsRequest.MaxResults = 50;

            PlaylistItemListResponse results;
            do
            {
                results = await itemsRequest.ExecuteAsync();
                foreach (var item in results.Items)
                {
                    playlistItems.Add(item.ContentDetails.VideoId, item.Snippet.Title);
                }
                itemsRequest.PageToken = results.NextPageToken;
            }
            while (results.NextPageToken != null);

            var shuffled = Shuffle(playlistItems.Keys.ToList());

            var newPlaylist = new Playlist();
            newPlaylist.Snippet = new PlaylistSnippet();
            newPlaylist.Snippet.Title = "Workout Mix." + DateTime.Now.ToString("yyyyMMdd");
            newPlaylist.Snippet.Description = "Re-shuffled Workout Playlist";
            newPlaylist.Status = new PlaylistStatus();
            newPlaylist.Status.PrivacyStatus = "public";
            newPlaylist = await youtubeService.Playlists.Insert(newPlaylist, "snippet,status").ExecuteAsync();

            var newPlaylistItem = new PlaylistItem();
            newPlaylistItem.Snippet = new PlaylistItemSnippet();
            newPlaylistItem.Snippet.PlaylistId = newPlaylist.Id;
            newPlaylistItem.Snippet.ResourceId = new ResourceId();
            newPlaylistItem.Snippet.ResourceId.Kind = "youtube#video";

            foreach (var shuffleItem in shuffled)
            {
                newPlaylistItem.Snippet.ResourceId.VideoId = shuffleItem;
                newPlaylistItem = await youtubeService.PlaylistItems.Insert(newPlaylistItem, "snippet").ExecuteAsync();
            }

            Console.WriteLine($"Successfully shuffled {shuffled.Count} items into new playlist \"{newPlaylist.Snippet.Title}\"");
        }

        private static List<string> Shuffle(List<string> items)
        {
            var output = new string[items.Count];
            var rand = new Random();

            for (var i = 0; i < items.Count; i++)
            {
                var next = rand.Next(0, items.Count);
                if (output[next] == null)
                {
                    output[next] = items[i];
                }
                else
                {
                    i--;
                }
            }

            return output.ToList();
        }
    }
}
