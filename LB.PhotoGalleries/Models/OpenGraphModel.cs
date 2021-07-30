using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace LB.PhotoGalleries.Models
{
    public class OpenGraphModel
    {
        public string Title { get; set; }

        public string Description { get; set; }
        public string SiteName { get; set; }
        public string Locale { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }
        public List<OpenGraphImageModel> Images { get; set; }
        public string FbAppId => "131542586870292";

        public OpenGraphModel()
        {
            Images = new List<OpenGraphImageModel>();

            // default values
            Title = "LB Photos";
            Description = "Publishing the world's best motorcycle photos.";
            SiteName = "LB Photos";
            Locale = "en_gb";
            Type = OpenGraphTypes.Website;
        }

        public static class OpenGraphTypes
        {
            public static string Article => "article";
            public static string Book => "book";
            public static string MusicAlbum => "music.album";
            public static string MusicPlaylist => "music.playlist";
            public static string MusicRadioStation => "music.radio_station";
            public static string MusicSong => "music.song";
            public static string Profile => "profile";
            public static string VideoEpisode => "video.episode";
            public static string VideoMovie => "video.movie";
            public static string VideoOther => "video.other";
            public static string VideoTvShow => "video.tv_show";
            public static string Website => "website";
        }

        public static class OpenGraphImageContentTypes
        {
            public static string Jpeg => "image/jpeg";
            public static string Png => "image/png";
        }

        public class OpenGraphImageModel
        {
            public string Url { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
            public string ContentType { get; set; }
        }
    }
}
